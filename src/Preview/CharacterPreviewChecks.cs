using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using FalloutRemake.Import;
using FalloutRemake.Runtime;
using FalloutRemake.Runtime.Combat;
using Godot;

namespace FalloutRemake.Preview;

public partial class TemplePreview
{
    private async Task CheckCharacterCreation()
    {
        _checkingNavigation = true;
        try
        {
            ResetWalker();
            if (_premadeChoices.Count != 3 || !_premadeChoices.Select(p => p.Character.Name).SequenceEqual(new[] { "Narg", "Mingan", "Chitsa" }))
                throw new InvalidDataException("The three original premade characters were not loaded in the original order.");
            using var key = new InputEventKey { Pressed = true, PhysicalKeycode = Key.N };
            _UnhandledInput(key);
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            CharacterCreationWindow window = _characterWindow ?? throw new InvalidDataException("N did not open the character window.");
            // The current neutral profile is preselected; each premade shows its original sheet and biography.
            if (window.Selected != PlayerCharacter.Neutral || !window.CanStart) throw new InvalidDataException("The current character was not preselected.");
            window.Select(0);
            if (!window.SheetText.Contains("Narg") || !window.SheetText.Contains("HP 44") || !window.SheetText.Contains("Heavy Handed") || !window.SheetText.Contains("Gifted")
                || !window.SheetText.Contains("Melee Weapons* 64%") || !window.SheetText.Contains("NARG'S STORY", StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("Narg's sheet or biography differs: " + window.SheetText);
            if (_capture != null) await Capture(Path.ChangeExtension(_capture, "character-premade.png"));
            window.Select(2);
            if (!window.SheetText.Contains("Speech* 70%") || !window.SheetText.Contains("Sex Appeal")) throw new InvalidDataException("Chitsa's sheet differs.");
            // Creation: the editor refuses to start until 40 points, 3 tags and a name; extra traits and tags are greyed out.
            window.Select(window.CreateIndex);
            if (window.CanStart || !window.ProblemText.Contains("nome")) throw new InvalidDataException("A nameless character could start: " + window.ProblemText);
            window.SetCharacterName("Teste");
            if (window.CanStart || !window.ProblemText.Contains("ponto")) throw new InvalidDataException("Unspent points were accepted: " + window.ProblemText);
            window.SetSpecial(5, 10);
            window.SetTrait(CharacterTrait.SmallFrame, true); window.SetTrait(CharacterTrait.Finesse, true);
            if (window.TraitEnabled(CharacterTrait.Gifted)) throw new InvalidDataException("A third trait was offered.");
            window.SetTag(CharacterSkill.Lockpick, true); window.SetTag(CharacterSkill.Sneak, true);
            if (window.CanStart || !window.ProblemText.Contains("3")) throw new InvalidDataException("Two tags were accepted.");
            window.SetTag(CharacterSkill.Speech, true);
            if (window.TagEnabled(CharacterSkill.Steal) || !window.CanStart || window.Selected is not { Kind: CharacterKind.Created } created)
                throw new InvalidDataException("The complete character could not start: " + window.ProblemText);
            if (_capture != null) await Capture(Path.ChangeExtension(_capture, "character-create.png"));
            window.EmitSignal(AcceptDialog.SignalName.Confirmed);
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            if (_characterWindow != null || _campaign!.Character != created || _campaign.Experience != 0 || _campaign.CarryCapacity != created.CarryWeight
                || _skills.Character.Lockpick != created.Skill(CharacterSkill.Lockpick) || _skills.Character.Lockpick != 10 + 5 + 10 + 20
                || _campaign.Location.Hex != _map!.Prefix.Header.EnteringTile)
                throw new InvalidDataException("Starting did not reset the world with the created character.");
            // The checkpoint keeps who plays.
            SaveCheckpoint(); var saved = WorldCheckpoint(); ResetWalker(); LoadCheckpoint();
            if (saved.Version != 12 || saved.Character is not { Kind: CharacterKind.Created, Name: "Teste" } || _campaign.Character.Name != "Teste"
                || _skills.Character.Lockpick != created.Skill(CharacterSkill.Lockpick))
                throw new InvalidDataException("Checkpoint v12 lost the created character.");
            // Narg: original sheet in the world; without a Speech tag the Temple dialogue explains the duel it would take.
            OpenCharacterWindow(); _characterWindow!.Select(0); _characterWindow.EmitSignal(AcceptDialog.SignalName.Confirmed);
            if (_campaign.Character.Name != "Narg" || _campaign.MaxHitPoints != 44 || _campaign.CarryCapacity != 250 || _campaign.Character.QualifiesForTempleSpeech)
                throw new InvalidDataException("Narg did not start with his original sheet.");
            DerivedCombatStats narg = DerivedCombatStats.ForPlayer(_campaign.Character, _campaign.Level);
            if (narg.MaxActionPoints != 9 || narg.MeleeDamage != 8 || narg.Skill(DerivedCombatStats.MeleeWeapons) != 64) throw new InvalidDataException("Narg's combat stats differ.");
            // Back to the profile every other check uses.
            OpenCharacterWindow(); _characterWindow!.Select(_characterWindow.NeutralIndex); _characterWindow.EmitSignal(AcceptDialog.SignalName.Confirmed);
            if (_campaign.Character != PlayerCharacter.Neutral || _skills.Character.Lockpick != 20) throw new InvalidDataException("The neutral profile did not return.");
            foreach (WorldArea area in _campaign.Areas) MapLayoutWriter.Verify(area.Map);
            ResetWalker();
            GD.Print("CHARACTER_CREATION_OK: premade Narg/Mingan/Chitsa from original GCD and biographies, editor limits (40 points, 2 traits, 3 tags), new game reset, created character saved in checkpoint v12, Narg's sheet in the world and Speech gate, neutral profile restored; source bytes verified.");
        }
        finally { CloseCharacterWindow(); _checkingNavigation = false; }
    }
    private WorldCheckpoint WorldCheckpoint() => CheckpointFile.Read(CheckpointPath);
}
