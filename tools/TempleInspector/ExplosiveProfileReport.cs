using FalloutRemake.Import;
using FalloutRemake.Runtime;

internal static class ExplosiveProfileReport
{
    public static object Create(GameData data)
    {
        MapDocument map = MapDocument.Read(data.Read("maps/arcaves.map"), data.Read); MapLayoutWriter.Verify(map);
        MapObject owner = map.ObjectLayers.SelectMany(l => l.Objects).Single(o => o.Offset == 337852 && o.Header.Id == 358);
        var entry = owner.Inventory.Single(e => e.Object.Header.Id == 364 && e.Object.Header.Pid == 85);
        var resources = new ResourceCatalog(data.Read); var prototypes = new PrototypeCatalog(resources);
        Prototype inactive = prototypes.Get(85), active = prototypes.Get(209);
        if (inactive.Type != 0 || active.Type != 0 || inactive.Subtype != 5 || active.Subtype != 5 || inactive.ScriptId != -1 || active.ScriptId != -1)
            throw new InvalidDataException("Unexpected plastic explosive prototype profile.");
        return new { Status = "EXPLOSIVE_PROFILE_OK", SourceItem = new { entry.Object.Key, entry.Object.Header.Id, entry.Object.Header.Pid, entry.Quantity },
            Inactive = new { inactive.Pid, inactive.Resource, inactive.Fid, inactive.InventoryFid, inactive.BaseWeight },
            Active = new { active.Pid, active.Resource, active.Fid, active.InventoryFid, active.BaseWeight },
            Timer = new { Minimum = 10, Maximum = 180, Default = 60, Step = 10, TicksPerSecond = 10, Skill = "Traps, modifier zero; Demolition Expert forces success" },
            DefaultSchedules = Enum.GetValues<SkillRollOutcome>().Select(outcome => new { Outcome = outcome.ToString(), Schedule = ExplosiveTimer.Plan(60, outcome) }),
            Ownership = "Confirmed arm path returns 2; _obj_use_item removes one carried unit and connects it at the owner's tile/elevation",
            Expiry = "Explosion uses the current owner position if carried, otherwise the ground position; destroys the object unless actionExplode returns busy (-2), which requeues 50 ticks",
            PendingReferenceIssues = new[] { "Pinned CE activates PID 85 to 209, but explosiveGetDamage only recognizes built-in inactive PID 85; queue does not check the helper's false result. Active damage mapping must be resolved explicitly.",
                "Pinned _obj_use_explosive returns 2 after timer cancellation too; the wrapper can drop an unarmed unit. Cancellation behavior must be chosen explicitly before a playable adapter." },
            AdditionalDependencies = resources.Identities, RuntimeActivationEnabled = false, OriginalReconstruction = "verified" };
    }
}
