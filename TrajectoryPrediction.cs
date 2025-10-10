// Not really worth it.
// I can't get consistent results with different kinds of weapons
// A mech inferno turret will always hit moving target PERFECTLY 40 tiles away.
// Then we got molotovs - target runs past predicted position just fine.
// Maybe the problem is that I'm trying to apply a solution filled with floats for an Int vector based game
// And also short range of frag grenades and very long fuse makes this system useless for them
// A better approach might be taking known predetermined position from pawn pathfinding
// But that just seems unrealistic in many scenarios
// Grenades are meant to be thrown at stationary targets anyway.
// I'll leave this code here for now, maybe someone else can refine it.

/*
using HarmonyLib;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using Verse;
using Verse.AI;
using static UnityEngine.GraphicsBuffer;

namespace BetterGrenadeHandling
{
    // Automatically lead the target by moving usedTarget position in advance to pawn's movement whenever explosive projectile is launched
    [HarmonyPatch(typeof(Projectile))]
    [HarmonyPatch("Launch")]
    [HarmonyPatch(new Type[] {
    typeof(Thing), typeof(Vector3), typeof(LocalTargetInfo), typeof(LocalTargetInfo), typeof(ProjectileHitFlags), typeof(bool), typeof(Thing), typeof(ThingDef)
    })]
    public static class Patch_Projectile_Launch_Prefix
    {
        static void Prefix(Projectile __instance, Thing launcher, Vector3 origin, ref LocalTargetInfo usedTarget, LocalTargetInfo intendedTarget, ProjectileHitFlags hitFlags,
            bool preventFriendlyFire = false, Thing equipment = null, ThingDef targetCoverDef = null)
        {
            if (!(__instance is Projectile_Explosive explosive))
            {
                return;
            }
            Pawn launcherPawn = launcher as Pawn;

            // Return if there is no pawn or pawn pather in intended target
            Pawn target = intendedTarget.Pawn;
            Pawn_PathFollower pawnPather = target?.pather ?? null;
            if (target == null || pawnPather == null)
            {
                return;
            }

            if (!pawnPather.Moving)
            {
                return;
            }

            IntVec3 launcherPosition = launcher.PositionHeld;
            IntVec3 targetPosition = target.PositionHeld;
            IntVec3 nextCell = pawnPather.nextCell; 
            float angle = (nextCell - target.Position).ToVector3().AngleFlat();
            // Get appropriate diagonal/cardinal speed for next cell
            float ticksPerCell = ((nextCell.x != targetPosition.x && nextCell.z != targetPosition.z) ? target.TicksPerMoveDiagonal : target.TicksPerMoveCardinal);
            float cellsPerTick = 1f / ticksPerCell;

            IntVec3 direction =  nextCell - targetPosition;
            Vector3 targetVelocity = direction.ToVector3() * (cellsPerTick);

            float distance = IntVec3Utility.DistanceTo(launcherPosition, targetPosition);
            float projectile_CellsPerTick = explosive.def.projectile.SpeedTilesPerTick;
            IntVec3 projectileDisplacement = targetPosition - launcherPosition;
            Vector3 projectileVelocity = projectileDisplacement.ToVector3() * (projectile_CellsPerTick / distance);

            float ticksToDetonate = explosive.def.projectile.explosionDelay;

            float tx = targetPosition.x - launcherPosition.x;
            float tz = targetPosition.z - launcherPosition.z;
            float tvx = targetVelocity.x;
            float tvz = targetVelocity.z;

            // Get quadratic equation components
            float a = tvx * tvx + tvz * tvz - projectile_CellsPerTick * projectile_CellsPerTick;
            float b = 2 * (tvx * tx + tvz * tz);
            float c = tx * tx + tz * tz;

            // Solve quadratic
            float quad1 = 0f;
            float quad2 = 0f;
            if (Math.Abs(a) < 1e-6)
            {
                if (Math.Abs(b) < 1e-6)
                {
                    if (Math.Abs(c) < 1e-6)
                    {
                        quad1 = 0f;
                        quad2 = 0f;
                    }
                    else
                    {
                        // No solution
                        return;
                    }
                }
                else
                {
                    quad1 = -c / b;
                    quad2 = -c / b;
                }
            }
            else
            {
                double disc = b * b - 4 * a * c;
                if (disc >= 0)
                {
                    disc = Math.Sqrt(disc);
                    a = 2 * a;
                    quad1 = (-b - (float)disc) / a;
                    quad2 = (-b + (float)disc) / a;
                }
            }

            // Find smallest positive solution
            float time = Math.Min(quad1, quad2);
            if (time < 0) time = Math.Max(quad1, quad2);
            Vector3 aimPos = targetPosition.ToVector3() + (targetVelocity * (time + ticksToDetonate));
            IntVec3 intAimPos = new IntVec3((int)Math.Round(aimPos.x), 0, (int)Math.Round(aimPos.z));

            // Create new target info
            // i know, i know, no forced miss, but that was just for testing
            LocalTargetInfo newtarget = new LocalTargetInfo(intAimPos);
            usedTarget = newtarget;

            Log.Message($"evaluation for target {target.LabelShort}: position {targetPosition}, nextCell {nextCell}, ticksPerCell {ticksPerCell}, direction {direction}, "
                + $"targetVelocity {targetVelocity}, projectileVelocity {projectileVelocity}, ticksToDetonate {ticksToDetonate}. Result: intAimPos {intAimPos}, time {time}.");
        }
    }
}
*/