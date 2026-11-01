using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using FalloutRemake.Geometry;
using FalloutRemake.Import;
using FalloutRemake.Runtime;
using Godot;

namespace FalloutRemake.Preview;

public partial class TemplePreview
{
    /// <summary>
    /// Runs after the first arrival in Arroyo. H opens the sheet of the character in play: level-up points bought with plus, refunded with minus, applied by Done,
    /// dropped by Cancel, kept by checkpoint v13 and used by lockpicking. The world is restored afterwards.
    /// </summary>
    private async Task CheckSkillSheet()
    {
        bool checking = _checkingNavigation; _checkingNavigation = true;
        string catalogHash = _mapCatalog!.Source.Sha256;
        WorldCheckpoint baseline = _campaign!.Capture(catalogHash);
        try
        {
            // After the first Arroyo arrival (625 XP), every Temple creature dead where it stands (840 XP): a fought-through run at level 2.
            WorldArea temple = _campaign.Areas.Single(a => a.Map.Prefix.Header.MapIndex == 3);
            DefeatedCritter[] corpses = temple.Map.ObjectLayers.SelectMany(l => l.Objects)
                .Where(o => WorldSession.EncounterPids.Contains(o.Header.Pid) && HexNavigation.Valid(o.Header.Tile) && (o.Header.Flags & HexNavigation.Hidden) == 0)
                .Select(o => new DefeatedCritter(3, o.Key, o.Header.Pid, o.Header.Elevation, o.Header.Tile)).ToArray();
            int xp = corpses.Sum(c => CritterPrototypeData.Read(temple.Map.Prototypes.Single(p => p.Pid == c.Pid)).Experience);
            int level = CharacterProgression.Level(_campaign.Experience + xp);
            if (level < 2) throw new InvalidDataException($"The Temple creatures award {xp} XP, short of level 2.");
            int points = Math.Min(CharacterProgression.MaxUnspentSkillPoints, (level - 1) * _campaign.Character.SkillPointsPerLevel);
            CheckpointFile.Write(CheckpointPath, baseline with { Combat = new SavedCombat(corpses, xp, null), Skills = new SavedSkills(new int[CharacterSkill.Count], points) });
            LoadCheckpoint();
            if (_campaign.Level != level || _campaign.UnspentSkillPoints != points || !_hud.CharacterText.Contains($"{points} pontos de perícia [H]"))
                throw new InvalidDataException("The leveled world did not offer its skill points.");
            int lockpick = _skills.Character.Lockpick;
            using (var key = new InputEventKey { Pressed = true, PhysicalKeycode = Key.H }) _UnhandledInput(key);
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            SkillSheetWindow sheet = _skillSheet ?? throw new InvalidDataException("H did not open the sheet.");
            if (sheet.Subtract(CharacterSkill.Lockpick) != SkillSpendStatus.AtMinimum || !sheet.MessageText.Contains("abriu"))
                throw new InvalidDataException("Minus refunded below the opening value.");
            for (int i = 0; i < 4; i++) sheet.Add(CharacterSkill.Lockpick);
            sheet.Subtract(CharacterSkill.Lockpick);
            if (sheet.Spending.Unspent != points - 3 || !sheet.SkillText(CharacterSkill.Lockpick).Contains($"{lockpick + 3}%") || _campaign.UnspentSkillPoints != points)
                throw new InvalidDataException("Plus and minus did not stage the spending: " + sheet.SkillText(CharacterSkill.Lockpick));
            if (_capture != null) await Capture(Path.ChangeExtension(_capture, "skill-sheet.png"));
            sheet.EmitSignal(AcceptDialog.SignalName.Confirmed);
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            if (_skillSheet != null || _skills.Character.Lockpick != lockpick + 3 || _campaign.UnspentSkillPoints != points - 3)
                throw new InvalidDataException("Done did not apply the spending to the world and the lockpick roll.");
            SaveCheckpoint(); WorldCheckpoint saved = WorldCheckpoint(); ResetWalker(); LoadCheckpoint();
            if (saved.Version != 13 || saved.Skills?.Raises[CharacterSkill.Lockpick] != 3 || saved.Skills.Unspent != points - 3
                || _skills.Character.Lockpick != lockpick + 3 || _campaign.UnspentSkillPoints != points - 3)
                throw new InvalidDataException("Checkpoint v13 lost the spending.");
            // A later opening cannot take back what was bought; Cancel drops a staged purchase.
            OpenSkillSheet(); sheet = _skillSheet!;
            if (sheet.Subtract(CharacterSkill.Lockpick) != SkillSpendStatus.AtMinimum) throw new InvalidDataException("An earlier purchase was refunded.");
            sheet.Add(CharacterSkill.Sneak); sheet.EmitSignal(AcceptDialog.SignalName.Canceled);
            if (_skillSheet != null || _campaign.UnspentSkillPoints != points - 3) throw new InvalidDataException("Cancel applied the staged purchase.");
            foreach (WorldArea area in _campaign.Areas) MapLayoutWriter.Verify(area.Map);
            GD.Print($"SKILL_SHEET_OK: level {level} with {points} points, H sheet, minus refused at the opening value, plus/minus staged, Done applied Lockpick {lockpick}->{lockpick + 3}, checkpoint v13 kept it, later minus refused, Cancel dropped; source bytes verified.");
        }
        finally
        {
            CloseSkillSheet(); _checkingNavigation = checking;
            CheckpointFile.Write(CheckpointPath, baseline); LoadCheckpoint();
        }
    }
}
