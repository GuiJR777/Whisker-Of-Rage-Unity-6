// Purpose: Sandbox cross-package test: BT → command buffers → HFSM (External owners) and player ↔ AI source swap.
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;

using NUnit.Framework;

using UnityEngine;
using UnityEngine.TestTools;

using RamiresTechGames.AI.Blackboards;
using RamiresTechGames.AI.Commands;
using RamiresTechGames.AI.Components;
using RamiresTechGames.AI.Definitions;
using RamiresTechGames.AI.Integration.Character;
using RamiresTechGames.AI.Integration.Combat;
using RamiresTechGames.AI.Trees;
using RamiresTechGames.AI.Trees.Composites;
using RamiresTechGames.Character.Commands;
using RamiresTechGames.Character.Components;
using RamiresTechGames.Character.Definitions;
using RamiresTechGames.Character.Facing;
using RamiresTechGames.Character.Integration.HierarchicalStateMachine;
using RamiresTechGames.Combat.Commands;
using RamiresTechGames.Combat.Components;
using RamiresTechGames.Combat.Definitions;
using RamiresTechGames.Combat.Integration.HierarchicalStateMachine;
using RamiresTechGames.HierarchicalStateMachine.Behaviours;
using RamiresTechGames.HierarchicalStateMachine.Components;
using RamiresTechGames.HierarchicalStateMachine.Definitions;
using RamiresTechGames.HierarchicalStateMachine.Execution;
using RamiresTechGames.Stats.Components;
using RamiresTechGames.Stats.Definitions;

using Object = UnityEngine.Object;

namespace RamiresTechGames.Sandbox.Tests.Integration.AI
{
    /// <summary>Records the fixed step of every tick and succeeds.</summary>
    [Serializable]
    internal sealed class StepMark : BehaviourNode
    {
        internal static readonly List<long> STEPS = new List<long>();

        public override NodeStatus OnTick(BehaviourContext context)
        {
            STEPS.Add(context.StepIndex);
            return NodeStatus.Success;
        }
    }

    /// <summary>
    /// Specification §12.5 and AC2: the AI never touches the HFSM. The same prefab and the same
    /// <see cref="StateMachineDefinition"/> (Combat/Character states with External owners) are driven by a player-like
    /// source or by an <see cref="AIAgent"/>; with <see cref="EvaluationLoop.FixedUpdate"/> the edge written at
    /// <c>DECISION + 20</c> is claimed and executed in the same fixed step.
    /// </summary>
    internal sealed class AiStateMachinePatternPlayModeTests
    {
        private const float FIXED_DELTA_TIME = 0.02f;
        private const string GROUNDED = "Grounded";
        private const string ATTACK = "Attack";
        private const ulong ID = 1UL;

        private readonly List<Object> _created = new List<Object>();
        private float _previousFixedDeltaTime;
        private AIWorld _world;
        private StatSetDefinition _statSet;
        private ResourceDefinition _health;
        private CombatActionDefinition _light;
        private AttackDefinition _attack;
        private StateMachineDefinition _definition;
        private List<string> _log;

        private static FieldInfo Field(object target, string name)
        {
            for (Type type = target.GetType(); type != null; type = type.BaseType)
            {
                FieldInfo field = type.GetField(name, BindingFlags.NonPublic | BindingFlags.Instance);
                if (field != null)
                {
                    return field;
                }
            }

            throw new MissingFieldException(target.GetType().Name, name);
        }

        private static void Set(object target, string name, object value)
        {
            Field(target, name).SetValue(target, value);
        }

        private static T Get<T>(object target, string name)
        {
            return (T)Field(target, name).GetValue(target);
        }

        private T Asset<T>(string name) where T : ScriptableObject
        {
            T asset = ScriptableObject.CreateInstance<T>();
            asset.name = name;
            _created.Add(asset);
            return asset;
        }

        private GameObject Track(GameObject gameObject)
        {
            _created.Add(gameObject);
            return gameObject;
        }

        [SetUp]
        public void SetUp()
        {
            _previousFixedDeltaTime = Time.fixedDeltaTime;
            Time.fixedDeltaTime = FIXED_DELTA_TIME;
            StepMark.STEPS.Clear();
            _log = new List<string>();
            StatDefinition maxHealth = Asset<StatDefinition>("MaxHealth");
            Set(maxHealth, "_stableId", Guid.NewGuid().ToString("N"));
            Set(maxHealth, "_defaultBaseValue", 100f);
            _health = Asset<ResourceDefinition>("Health");
            Set(_health, "_stableId", Guid.NewGuid().ToString("N"));
            Set(_health, "_maxStat", maxHealth);
            _statSet = Asset<StatSetDefinition>("Stats");
            Get<List<StatEntry>>(_statSet, "_stats").Add(new StatEntry(maxHealth));
            Get<List<ResourceEntry>>(_statSet, "_resources").Add(new ResourceEntry(_health));
            _light = Asset<CombatActionDefinition>("Light");
            _attack = Asset<AttackDefinition>("Attack");
            Set(_attack, "_duration", 0.2f);
            _definition = Definition();
            GameObject world = Track(new GameObject("AIWorld"));
            _world = world.AddComponent<AIWorld>();
            GameObject floor = Track(new GameObject("Floor"));
            floor.transform.position = new Vector3(0f, -0.5f, 0f);
            floor.AddComponent<BoxCollider>().size = new Vector3(100f, 1f, 100f);
        }

        [TearDown]
        public void TearDown()
        {
            for (int index = _created.Count - 1; index >= 0; index--)
            {
                if (_created[index] != null)
                {
                    Object.DestroyImmediate(_created[index]);
                }
            }

            _created.Clear();
            BehaviourTreeDefinition.InvalidateLayouts();
            Time.fixedDeltaTime = _previousFixedDeltaTime;
        }

        private StateMachineDefinition Definition()
        {
            StateMachineDefinition definition = Asset<StateMachineDefinition>("Combatant (sandbox)");
            List<StateData> states = Get<List<StateData>>(definition, "_states");
            List<TransitionData> transitions = Get<List<TransitionData>>(definition, "_transitions");
            states.Add(State(GROUNDED, true, new LocomotionBehaviour()));
            states.Add(State(ATTACK, false, new AttackBehaviour()));
            transitions.Add(Transition(GROUNDED, ATTACK, 10, new HasCombatActionCommandCondition()));
            transitions.Add(Transition(ATTACK, GROUNDED, 0, new StateCompletedCondition()));
            return definition;
        }

        private static StateData State(string name, bool isInitial, StateBehaviour behaviour)
        {
            StateData state = new StateData();
            Set(state, "_id", name);
            Set(state, "_name", name);
            Set(state, "_parentId", string.Empty);
            Set(state, "_isInitial", isInitial);
            Set(state, "_behaviour", behaviour);
            return state;
        }

        private static TransitionData Transition(string from, string to, int priority,
            params TransitionCondition[] conditions)
        {
            TransitionData transition = new TransitionData();
            Set(transition, "_id", from + "->" + to);
            Set(transition, "_fromId", from);
            Set(transition, "_toId", to);
            Set(transition, "_priority", priority);
            Set(transition, "_conditions", new List<TransitionCondition>(conditions));
            return transition;
        }

        private BehaviourTreeDefinition Tree()
        {
            BehaviourTreeDefinition tree = Asset<BehaviourTreeDefinition>("Attack once");
            tree.SetBlackboard(Asset<BlackboardDefinition>("Blackboard"));
            NodeData root = tree.AddNode(null, new SequenceNode());
            tree.AddNode(root.Id, new StepMark());
            tree.AddNode(root.Id, new PerformCombatAction(_light, CompleteOn.Ended));
            tree.SetRestartPolicy(TreeRestartPolicy.StopOnComplete);
            return tree;
        }

        /// <summary>
        /// One prefab-like actor: real motor and combat (action owner External), an HFSM runner evaluating in the
        /// fixed step with the shared definition, and an AI agent with HFSM-owner bridges.
        /// </summary>
        private AIAgent Actor(BehaviourTreeDefinition tree, bool takeControlOnEnable)
        {
            GameObject actor = Track(new GameObject("Combatant"));
            actor.SetActive(false);
            Rigidbody body = actor.AddComponent<Rigidbody>();
            body.constraints = RigidbodyConstraints.FreezeRotation;
            CapsuleCollider capsule = actor.AddComponent<CapsuleCollider>();
            capsule.radius = 0.4f;
            capsule.height = 2f;
            capsule.center = new Vector3(0f, 1f, 0f);
            HurtBox hurtBox = actor.AddComponent<HurtBox>();
            StatsComponent stats = actor.AddComponent<StatsComponent>();
            Set(stats, "_statSet", _statSet);
            CombatProfileDefinition profile = Asset<CombatProfileDefinition>("Profile");
            Set(profile, "_healthResource", _health);
            Set(profile, "_team", 1);
            Set(profile, "_inputBufferTime", 0.5f);
            CombatCommandBuffer combatBuffer = actor.AddComponent<CombatCommandBuffer>();
            CombatTarget target = actor.AddComponent<CombatTarget>();
            Set(target, "_profile", profile);
            Set(target, "_stats", stats);
            Set(target, "_combatantId", ID);
            Set(hurtBox, "_target", target);
            CombatController controller = actor.AddComponent<CombatController>();
            Set(controller, "_commandBuffer", combatBuffer);
            Set(controller, "_actionOwner", CombatActionOwner.External);
            CombatMove move = new CombatMove();
            Set(move, "_action", _light);
            Set(move, "_attack", _attack);
            Get<List<CombatMove>>(controller, "_moves").Add(move);
            actor.AddComponent<CharacterCommandBuffer>();
            CharacterMotor motor = actor.AddComponent<CharacterMotor>();
            Set(motor, "_profile", Asset<MovementProfileDefinition>("Movement"));
            actor.AddComponent<FacingController>();
            StateMachineRunner runner = actor.AddComponent<StateMachineRunner>();
            Set(runner, "_definition", _definition);
            Set(runner, "_evaluationLoop", EvaluationLoop.FixedUpdate);
            AIPerceivable perceivable = actor.AddComponent<AIPerceivable>();
            perceivable.SetSerializedEntityId(ID);
            perceivable.SetWorld(_world);
            actor.AddComponent<AITeam>().Configure(1, 1u);
            AIAgent agent = actor.AddComponent<AIAgent>();
            agent.Configure(_world, tree);
            Set(agent, "_takeControlOnEnable", takeControlOnEnable);
            CombatAgentBridge combat = actor.AddComponent<CombatAgentBridge>();
            Set(combat, "_path", ExecutionPath.HfsmOwner);
            actor.AddComponent<CharacterAgentLocomotion>();
            controller.AttackStarted += args => _log.Add(_world.StepIndex + " Started " + args.Instance);
            controller.AttackEnded += args => _log.Add("Ended " + args.Instance + " " + args.Reason);
            actor.SetActive(true);
            return agent;
        }

        private static IEnumerator Until(Func<bool> condition, int maxSteps = 100)
        {
            for (int step = 0; step < maxSteps && !condition(); step++)
            {
                yield return new WaitForFixedUpdate();
            }
        }

        private static string Leaf(AIAgent agent)
        {
            return agent.GetComponent<StateMachineRunner>().Instance.ActiveLeafName;
        }

        [UnityTest]
        public IEnumerator TreeEdge_IsClaimedByTheStateMachine_InTheSameFixedStep_AndTrackedByToken()
        {
            AIAgent agent = Actor(Tree(), true);
            CombatAgentBridge bridge = agent.GetComponent<CombatAgentBridge>();
            List<NodeStatus> results = new List<NodeStatus>();
            List<string> states = new List<string>();
            agent.TreeCompleted += args => results.Add(args.Result);

            for (int step = 0; step < 60 && results.Count == 0; step++)
            {
                yield return new WaitForFixedUpdate();
                string leaf = Leaf(agent);
                if (states.Count == 0 || states[states.Count - 1] != leaf)
                {
                    states.Add(leaf);
                }
            }

            Assert.That(results, Is.EqualTo(new[] { NodeStatus.Success }), "attack started and ended normally");
            Assert.That(StepMark.STEPS.Count, Is.EqualTo(1));
            Assert.That(_log, Is.EqualTo(new[] { StepMark.STEPS[0] + " Started 1", "Ended 1 Completed" }),
                "the edge written at DECISION + 20 is claimed and executed in the same fixed step");
            Assert.That(states, Is.EqualTo(new[] { ATTACK, GROUNDED }),
                "the first fixed step already goes Grounded → Attack with the edge written by the agent");
            Assert.That(bridge.Actions.Tracker.UnconfirmedCount, Is.Zero);
            Assert.That(bridge.PathMismatch, Is.False);
            Assert.That(bridge.Buffer.Source, Is.SameAs(agent));
        }

        [UnityTest]
        public IEnumerator SameActorAndDefinition_SwitchBetweenPlayerAndAi_ClearingPendingEdges()
        {
            AIAgent agent = Actor(Tree(), false);
            CombatAgentBridge combat = agent.GetComponent<CombatAgentBridge>();
            CharacterAgentLocomotion locomotion = agent.GetComponent<CharacterAgentLocomotion>();
            StateMachineRunner runner = agent.GetComponent<StateMachineRunner>();
            int lost = 0;
            agent.ControlLost += args => lost++;
            object player = new object();
            combat.Buffer.SetSource(player);
            locomotion.Buffer.SetSource(player);
            yield return new WaitForFixedUpdate();

            Assert.That(agent.HasControl, Is.False, "not taken on enable");
            Assert.That(StepMark.STEPS, Is.Empty, "the tree waits for control");
            combat.Buffer.EnqueueAction(_light);
            yield return Until(() => Leaf(agent) == ATTACK);
            combat.Buffer.EnqueueAction(_light);
            Assert.That(combat.Buffer.PendingCount, Is.EqualTo(1), "player edge pending during the attack");

            agent.TakeControl();
            Assert.That(combat.Buffer.Source, Is.SameAs(agent));
            Assert.That(locomotion.Buffer.Source, Is.SameAs(agent));
            Assert.That(combat.Buffer.PendingCount, Is.Zero, "the player's pending edge is discarded");
            yield return Until(() => _log.Count >= 3);
            Assert.That(StepMark.STEPS.Count, Is.EqualTo(1), "the AI tree runs once control is taken");
            Assert.That(_log[2], Does.EndWith("Started 2"), "the AI attack runs through the same state machine");

            combat.Buffer.SetSource(player);
            locomotion.Buffer.SetSource(player);
            yield return new WaitForFixedUpdate();
            Assert.That(lost, Is.EqualTo(1));
            Assert.That(agent.HasControl, Is.False);
            Assert.That(agent.Tree.IsStopped, Is.True);
            yield return Until(() => Leaf(agent) == GROUNDED);
            combat.Buffer.EnqueueAction(_light);
            yield return Until(() => _log.Count >= 5);
            Assert.That(_log[4], Does.EndWith("Started 3"), "the player drives the same actor again");
            Assert.That(runner.Definition, Is.SameAs(_definition));
        }
    }
}
