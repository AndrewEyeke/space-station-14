using Content.Server.GameTicking.Rules.Components;
using Content.Server.StationEvents.Components;
using Content.Shared.Spawners.Components;
using Robust.Shared.Map;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;

namespace Content.Server.StationEvents.Events
{
    public sealed class MeteorSwarmRule : StationEventSystem<MeteorSwarmRuleComponent>
    {
        [Dependency] private readonly SharedPhysicsSystem _physics = default!;

        protected override void Started(EntityUid uid, MeteorSwarmRuleComponent component, GameRuleComponent gameRule, GameRuleStartedEvent args)
        {
            base.Started(uid, component, gameRule, args);

            var mod = Math.Sqrt(GetSeverityModifier());
            component._waveCounter = (int) (RobustRandom.Next(component.MinimumWaves, component.MaximumWaves) * mod);
        }

        protected override void ActiveTick(EntityUid uid, MeteorSwarmRuleComponent component, GameRuleComponent gameRule, float frameTime)
        {
            base.ActiveTick(uid, component, gameRule, frameTime);
            if (component._waveCounter <= 0)
            {
                ForceEndSelf(uid, gameRule);
                return;
            }

            var mod = GetSeverityModifier();

            component._cooldown -= frameTime;

            if (component._cooldown > 0f)
                return;

            component._waveCounter--;

            component._cooldown += (component.MaximumCooldown - component.MinimumCooldown) * RobustRandom.NextFloat() / mod + component.MinimumCooldown;

            Box2? playableArea = null;
            var mapId = GameTicker.DefaultMap;

            foreach (var grid in MapManager.GetAllMapGrids(mapId))
            {
                var aabb = _physics.GetWorldAABB(grid.Owner);
                playableArea = playableArea?.Union(aabb) ?? aabb;
            }

            if (playableArea == null)
            {
                ForceEndSelf(uid, gameRule);
                return;
            }

            var minimumDistance = (playableArea.Value.TopRight - playableArea.Value.Center).Length + 50f;
            var maximumDistance = minimumDistance + 100f;

            var center = playableArea.Value.Center;

            for (var i = 0; i < component.MeteorsPerWave; i++)
            {
                var angle = new Angle(RobustRandom.NextFloat() * MathF.Tau);
                var offset = angle.RotateVec(new Vector2((maximumDistance - minimumDistance) * RobustRandom.NextFloat() + minimumDistance, 0));
                var spawnPosition = new MapCoordinates(center + offset, mapId);
                var meteor = EntityManager.SpawnEntity("MeteorLarge", spawnPosition);
                var physics = EntityManager.GetComponent<PhysicsComponent>(meteor);
                _physics.SetBodyStatus(physics, BodyStatus.InAir);
                _physics.SetLinearDamping(physics, 0f);
                _physics.SetAngularDamping(physics, 0f);
                _physics.ApplyLinearImpulse(meteor, -offset.Normalized * component.MeteorVelocity * physics.Mass, body: physics);
                _physics.ApplyAngularImpulse(
                    meteor,
                    physics.Mass * ((component.MaxAngularVelocity - component.MinAngularVelocity) * RobustRandom.NextFloat() + component.MinAngularVelocity),
                    body: physics);

                EnsureComp<TimedDespawnComponent>(meteor).Lifetime = 120f;
            }
        }
    }
}
