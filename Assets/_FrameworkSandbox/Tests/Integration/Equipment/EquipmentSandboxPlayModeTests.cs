// Purpose: Sandbox cross-package test: equipment drives Character and Combat through Stats, weapon moves via HFSM.
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;

using NUnit.Framework;

using UnityEngine;
using UnityEngine.TestTools;

using RamiresTechGames.Abilities.Commands;
using RamiresTechGames.Abilities.Components;
using RamiresTechGames.Abilities.Definitions;
using RamiresTechGames.Abilities.Integration.Combat;
using RamiresTechGames.Abilities.Integration.HierarchicalStateMachine;
using RamiresTechGames.Character.Commands;
using RamiresTechGames.Character.Components;
using RamiresTechGames.Character.Definitions;
using RamiresTechGames.Character.Facing;
using RamiresTechGames.Character.Integration.Stats;
using RamiresTechGames.Combat.Commands;
using RamiresTechGames.Combat.Components;
using RamiresTechGames.Combat.Definitions;
using RamiresTechGames.Core.Tags;
using RamiresTechGames.Equipment.Components;
using RamiresTechGames.Equipment.Definitions;
using RamiresTechGames.Equipment.Integration.Abilities;
using RamiresTechGames.Equipment.Items;
using RamiresTechGames.Equipment.Transactions;
using RamiresTechGames.HierarchicalStateMachine.Behaviours;
using RamiresTechGames.HierarchicalStateMachine.Components;
using RamiresTechGames.HierarchicalStateMachine.Definitions;
using RamiresTechGames.Stats.Components;
using RamiresTechGames.Stats.Definitions;
using RamiresTechGames.Stats.Modifiers;
using RamiresTechGames.Stats.Values;

using Object = UnityEngine.Object;

namespace RamiresTechGames.Sandbox.Tests.Integration.Equipment
{
    /// <summary>
    /// Specification §12.3–§12.5 and §17.1 (Sandbox): Equipment has no Character/Combat assembly. Boots change the
    /// motor's speed through a stat and <c>StatMovementModifierSource</c>; a weapon raises the Combat attack power stat
    /// and grants a strike (ability with <c>PerformAttackAction</c>) that a state machine executes by input tag; the
    /// damage follows the weapon equipped, and unequipping removes the move.
    /// </summary>
    internal sealed class EquipmentSandboxPlayModeTests
    {
        private const float FIXED_DELTA_TIME = 0.02f;
        private const int CHARACTER_LAYER = 2;
        private const int HURTBOX_MASK = 1;
        private const float MAX_HEALTH = 100f;
        private const float BASE_SPEED = 6f;
        private const float BASE_ATTACK_POWER = 10f;
        private const int WEAPON = 0;
        private const int FEET = 1;
        private const string IDLE = "Idle";
        private const string STRIKE = "Strike";

        private readonly List<Object> _created = new List<Object>();
        private float _previousFixedDeltaTime;
        private StatDefinition _speed;
        private StatDefinition _attackPower;
        private ResourceDefinition _health;
        private StatSetDefinition _statSet;
        private EquipmentSlotLayoutDefinition _layout;
        private GameplayTag _attackInput;
        private ulong _nextItemId = 1UL;

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

        private StatDefinition Stat(string name, float baseValue)
        {
            StatDefinition stat = Asset<StatDefinition>(name);
            Set(stat, "_stableId", Guid.NewGuid().ToString("N"));
            Set(stat, "_defaultBaseValue", baseValue);
            return stat;
        }

        private GameObject Root(string name, Vector3 position, Vector3 facing)
        {
            GameObject root = new GameObject(name);
            _created.Add(root);
            root.SetActive(false);
            root.transform.SetPositionAndRotation(position, Quaternion.LookRotation(facing, Vector3.up));
            return root;
        }

        private static IEnumerator Steps(int count)
        {
            for (int step = 0; step < count; step++)
            {
                yield return new WaitForFixedUpdate();
            }
        }

        [SetUp]
        public void SetUp()
        {
            _previousFixedDeltaTime = Time.fixedDeltaTime;
            Time.fixedDeltaTime = FIXED_DELTA_TIME;
            StatDefinition maxHealth = Stat("MaxHealth", MAX_HEALTH);
            _speed = Stat("Speed", BASE_SPEED);
            _attackPower = Stat("AttackPower", BASE_ATTACK_POWER);
            _health = Asset<ResourceDefinition>("Health");
            Set(_health, "_stableId", Guid.NewGuid().ToString("N"));
            Set(_health, "_maxStat", maxHealth);
            _statSet = Asset<StatSetDefinition>("Stats");
            List<StatEntry> stats = Get<List<StatEntry>>(_statSet, "_stats");
            stats.Add(new StatEntry(maxHealth));
            stats.Add(new StatEntry(_speed));
            stats.Add(new StatEntry(_attackPower));
            Get<List<ResourceEntry>>(_statSet, "_resources").Add(new ResourceEntry(_health));
            EquipmentSlotDefinition weapon = Asset<EquipmentSlotDefinition>("Weapon");
            weapon.SetIdentity("slot-weapon");
            EquipmentSlotDefinition feet = Asset<EquipmentSlotDefinition>("Feet");
            feet.SetIdentity("slot-feet");
            _layout = Asset<EquipmentSlotLayoutDefinition>("Layout");
            _layout.AddEntry(weapon);
            _layout.AddEntry(feet);
            _attackInput = Asset<GameplayTag>("Input.Attack");
            Set(_attackInput, "_stableId", "tag-input-attack");
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
            Time.fixedDeltaTime = _previousFixedDeltaTime;
        }

        private EquipmentItem Item(string name, StatDefinition stat, float value)
        {
            EquipmentDefinition definition = Asset<EquipmentDefinition>(name);
            definition.SetIdentity("eq-" + name);
            definition.AddModifier(new EquipmentModifierEntry(stat, StatModifierOperation.Flat, value));
            return EquipmentItem.Create(definition, _nextItemId++);
        }

        private EquipmentDefinition Weapon(string name, float attackPower, AbilityDefinition strike)
        {
            EquipmentDefinition definition = Asset<EquipmentDefinition>(name);
            definition.SetIdentity("eq-" + name);
            definition.AddModifier(new EquipmentModifierEntry(_attackPower, StatModifierOperation.Flat, attackPower));
            definition.AddEffect(new GrantAbilityEffect(strike, _attackInput));
            return definition;
        }

        private AbilityDefinition Strike()
        {
            AttackDefinition attack = Asset<AttackDefinition>("Slash");
            Set(attack, "_duration", 0.3f);
            ActiveWindow window = new ActiveWindow();
            Set(window, "_start", 0f);
            Set(window, "_end", 0.1f);
            HitProperties properties = new HitProperties();
            Set(properties, "_damageMultiplier", 1f);
            Set(window, "_properties", properties);
            window.Hitboxes.Add(new HitboxShape());
            attack.ActiveWindows.Add(window);
            AbilityDefinition ability = Asset<AbilityDefinition>("Strike");
            Set(ability, "_duration", 0.3f);
            PerformAttackAction perform = new PerformAttackAction();
            Set(perform, "_attack", attack);
            AbilityActionEntry entry = new AbilityActionEntry();
            Set(entry, "_time", 0f);
            Set(entry, "_action", perform);
            Get<List<AbilityActionEntry>>(ability, "_actions").Add(entry);
            return ability;
        }

        private StateMachineDefinition Fighter()
        {
            StateMachineDefinition definition = Asset<StateMachineDefinition>("Fighter (sandbox)");
            HasAbilityCommandCondition command = new HasAbilityCommandCondition();
            Set(command, "_inputTag", _attackInput);
            Get<List<StateData>>(definition, "_states").AddRange(new[]
            {
                State(IDLE, true, null),
                State(STRIKE, false, new AbilityBehaviour())
            });
            Get<List<TransitionData>>(definition, "_transitions").AddRange(new[]
            {
                Transition(IDLE, STRIKE, 10, command),
                Transition(STRIKE, IDLE, 0, new StateCompletedCondition())
            });
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

        /// <summary>Combatant: hurtbox, stats, combat target/controller, ability system, Combat bridge.</summary>
        private GameObject Combatant(string name, ulong id, int team, Vector3 position, Vector3 facing)
        {
            GameObject root = Root(name, position, facing);
            Rigidbody body = root.AddComponent<Rigidbody>();
            body.isKinematic = true;
            CapsuleCollider capsule = root.AddComponent<CapsuleCollider>();
            capsule.isTrigger = true;
            capsule.radius = 0.4f;
            capsule.height = 2f;
            capsule.center = new Vector3(0f, 1f, 0f);
            HurtBox hurtBox = root.AddComponent<HurtBox>();
            StatsComponent stats = root.AddComponent<StatsComponent>();
            Set(stats, "_statSet", _statSet);
            CombatProfileDefinition profile = Asset<CombatProfileDefinition>("Profile " + team);
            Set(profile, "_healthResource", _health);
            Set(profile, "_team", team);
            Set(profile, "_hostileTeams", CombatProfileDefinition.DEFAULT_HOSTILE_TEAMS & ~(1 << team));
            Set(profile, "_attackPowerStat", _attackPower);
            CombatCommandBuffer commands = root.AddComponent<CombatCommandBuffer>();
            CombatTarget target = root.AddComponent<CombatTarget>();
            Set(target, "_profile", profile);
            Set(target, "_stats", stats);
            Set(target, "_combatantId", id);
            CombatController controller = root.AddComponent<CombatController>();
            Set(controller, "_commandBuffer", commands);
            Set(controller, "_hurtboxLayers", (LayerMask)HURTBOX_MASK);
            Set(hurtBox, "_target", target);
            AbilitySystemComponent system = root.AddComponent<AbilitySystemComponent>();
            Set(system, "_actorId", id);
            Set(system, "_stats", stats);
            root.AddComponent<CombatAbilityBridge>();
            return root;
        }

        private static float Health(GameObject actor, ResourceDefinition health)
        {
            StatCollection stats = actor.GetComponent<StatsComponent>().Stats;
            return stats.TryGetResource(health, out ResourcePool pool) ? pool.Current : -1f;
        }

        [UnityTest]
        public IEnumerator BootsSpeedStat_DrivesTheCharacterMotor_AndIsRestoredExactly()
        {
            GameObject floor = new GameObject("Floor");
            _created.Add(floor);
            floor.transform.position = new Vector3(0f, -0.5f, 0f);
            floor.AddComponent<BoxCollider>().size = new Vector3(400f, 1f, 400f);
            GameObject runner = Root("Runner", Vector3.zero, Vector3.right);
            runner.layer = CHARACTER_LAYER;
            Rigidbody body = runner.AddComponent<Rigidbody>();
            body.constraints = RigidbodyConstraints.FreezeRotation;
            CapsuleCollider capsule = runner.AddComponent<CapsuleCollider>();
            capsule.radius = 0.5f;
            capsule.height = 2f;
            capsule.center = new Vector3(0f, 1f, 0f);
            StatsComponent stats = runner.AddComponent<StatsComponent>();
            Set(stats, "_statSet", _statSet);
            CharacterCommandBuffer buffer = runner.AddComponent<CharacterCommandBuffer>();
            StatMovementModifierSource source = runner.AddComponent<StatMovementModifierSource>();
            Set(source, "_stats", stats);
            Set(source, "_speedStat", _speed);
            Set(source, "_speedReference", BASE_SPEED);
            CharacterMotor motor = runner.AddComponent<CharacterMotor>();
            Set(motor, "_profile", Asset<MovementProfileDefinition>("Movement"));
            Set(motor, "_groundMask", (LayerMask)1);
            Set(motor, "_modifierSource", source);
            runner.AddComponent<FacingController>();
            EquipmentComponent equipment = runner.AddComponent<EquipmentComponent>();
            equipment.Configure(_layout, stats);
            runner.SetActive(true);
            buffer.SetMove(Vector2.right);
            yield return Steps(40);
            float baseline = motor.LocomotionVelocity.magnitude;

            EquipResult boots = equipment.TryEquip(Item("Boots", _speed, 3f), FEET);
            yield return Steps(40);
            float hasted = motor.LocomotionVelocity.magnitude;
            equipment.TryUnequip(boots.Handle);
            yield return Steps(40);

            Assert.That(baseline, Is.EqualTo(BASE_SPEED).Within(0.01f));
            Assert.That(hasted, Is.EqualTo(BASE_SPEED + 3f).Within(0.01f), "Speed 9 / reference 6 = ×1.5");
            Assert.That(motor.LocomotionVelocity.magnitude, Is.EqualTo(BASE_SPEED).Within(0.01f));
            Assert.That(source.Current.SpeedMultiplier, Is.EqualTo(1f), "the stat is restored exactly");
        }

        [UnityTest]
        public IEnumerator WeaponGrantedStrike_RunsThroughTheStateMachine_AndDamageFollowsTheWeaponStat()
        {
            AbilityDefinition strike = Strike();
            GameObject fighter = Combatant("Fighter", 1UL, 1, Vector3.zero, Vector3.right);
            AbilityCommandBuffer commands = fighter.AddComponent<AbilityCommandBuffer>();
            AbilitySystemComponent system = fighter.GetComponent<AbilitySystemComponent>();
            Set(system, "_commandBuffer", commands);
            Set(system, "_commandOwner", AbilityCommandOwner.External);
            Set(system, "_inputBufferTime", 0.5f);
            StateMachineRunner runner = fighter.AddComponent<StateMachineRunner>();
            Set(runner, "_definition", Fighter());
            EquipmentComponent equipment = fighter.AddComponent<EquipmentComponent>();
            equipment.Configure(_layout, fighter.GetComponent<StatsComponent>());
            GameObject dummy = Combatant("Dummy", 2UL, 2, new Vector3(1.2f, 0f, 0f), Vector3.left);
            CombatController controller = fighter.GetComponent<CombatController>();
            List<string> attacks = new List<string>();
            controller.AttackStarted += args => attacks.Add("Started");
            fighter.SetActive(true);
            dummy.SetActive(true);
            yield return Steps(2);

            EquippedItemHandle sword = equipment.TryEquip(
                EquipmentItem.Create(Weapon("Sword", 5f, strike), _nextItemId++), WEAPON).Handle;
            commands.EnqueueActivation(_attackInput);
            yield return Steps(30);
            float afterSword = Health(dummy, _health);

            EquippedItemHandle axe = equipment.TrySwap(sword,
                EquipmentItem.Create(Weapon("Axe", 15f, strike), _nextItemId++)).Handle;
            commands.EnqueueActivation(_attackInput);
            yield return Steps(30);
            float afterAxe = Health(dummy, _health);

            equipment.TryUnequip(axe);
            commands.EnqueueActivation(_attackInput);
            yield return Steps(30);

            Assert.That(attacks.Count, Is.EqualTo(2), "one attack per weapon, none without a weapon");
            Assert.That(afterSword, Is.EqualTo(MAX_HEALTH - (BASE_ATTACK_POWER + 5f)).Within(1e-4f));
            Assert.That(afterAxe, Is.EqualTo(afterSword - (BASE_ATTACK_POWER + 15f)).Within(1e-4f));
            Assert.That(Health(dummy, _health), Is.EqualTo(afterAxe).Within(1e-4f));
            Assert.That(runner.Instance.ActiveLeafName, Is.EqualTo(IDLE));
            Assert.That(system.FindAbility(strike).IsValid, Is.False, "the move left with the weapon");
            Assert.That(fighter.GetComponent<StatsComponent>().Stats.GetValue(_attackPower),
                Is.EqualTo(BASE_ATTACK_POWER));
        }
    }
}
