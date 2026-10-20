using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FalloutRemake.Import;

namespace FalloutRemake.Runtime;

/// <summary>Original text for the bounded rehearsal, loaded locally and never embedded in a checkpoint.</summary>
public sealed class TempleSpeechText
{
    private readonly Dictionary<int, string> _messages;
    public ResourceIdentity Source { get; }
    public string Speaker => _messages[100];
    public TempleSpeechText(TempleSpeechProfile profile, MessageTextFile file, string language)
    {
        if (string.IsNullOrWhiteSpace(language) || language.Any(c => !char.IsAsciiLetterOrDigit(c) && c is not '_' and not '-')
            || file.Source.Path != $"text/{language}/dialog/actemvil.msg")
            throw new InvalidDataException("Speech text must use the selected original language and actor resource.");
        int[] ids = Enumerable.Range(0, 5).SelectMany(i => profile.Step(i).ReplyMessages.Append(profile.Step(i).ChoiceMessage)).Append(100).Distinct().ToArray();
        if (ids.Any(id => !file.Entries.TryGetValue(id, out var row) || string.IsNullOrWhiteSpace(row.Text)))
            throw new InvalidDataException("Original Speech rehearsal messages are incomplete.");
        Source = file.Source; _messages = ids.ToDictionary(id => id, id => file.Entries[id].Text);
    }
    public string Message(int id) => _messages.TryGetValue(id, out var text) ? text : throw new InvalidDataException("Message outside verified Speech route.");
    public string Reply(TempleSpeechStep step, string playerName)
    {
        if (string.IsNullOrWhiteSpace(playerName) || playerName.Length > 80 || playerName.Contains('\0'))
            throw new InvalidDataException("Invalid rehearsal character name.");
        return step.ReplyMessages.Length == 2 ? Message(step.ReplyMessages[0]) + playerName + Message(step.ReplyMessages[1])
            : step.ReplyMessages.Length == 1 ? Message(step.ReplyMessages[0]) : throw new InvalidDataException("Unsupported Speech reply.");
    }
}
