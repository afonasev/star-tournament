// Exact phrase/resource catalog; synchronized by tools/audio/generate_bot_banter.py.
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace StarTournament.ProvingGround
{
    public static class NativeBotBanterVoice
    {
        public static readonly IReadOnlyDictionary<string,string> Clips = new ReadOnlyDictionary<string,string>(
            new Dictionary<string,string>(StringComparer.Ordinal)
        {
            { "А вот и моя месть!", "Audio/Banter/revenge-1" },
            { "Всё, отомстил!", "Audio/Banter/revenge-2" },
            { "Убили-таки! А я только разошёлся!", "Audio/Banter/series-ended-1" },
            { "Ну всё, кончилась моя серия!", "Audio/Banter/series-ended-2" },
            { "Опять ты меня убил!", "Audio/Banter/repeated-killer-1" },
            { "Да отстань ты от меня!", "Audio/Banter/repeated-killer-2" },
            { "Опять меня убили!", "Audio/Banter/losing-streak-1" },
            { "Я играть пришёл, а не помирать!", "Audio/Banter/losing-streak-2" },
            { "Чуть не убил меня, зараза!", "Audio/Banter/narrow-win-1" },
            { "Еле выжил! Но ты-то помер!", "Audio/Banter/narrow-win-2" },
            { "Даже не поцарапал!", "Audio/Banter/dazhe-ne-pocarapal-radio" },
            { "Ты хоть попади сначала!", "Audio/Banter/missed-2" },
            { "Слабак!", "Audio/Banter/weak-reply-1" },
            { "Слабо бьёшь!", "Audio/Banter/weak-reply-2" },
            { "Попал. А толку?", "Audio/Banter/weak-reply-3" },
            { "Даже выстрелить не успел!", "Audio/Banter/no-reply-1" },
            { "Кто следующий?", "Audio/Banter/kill-streak-1" },
            { "Я сегодня в ударе!", "Audio/Banter/kill-streak-2" },
            { "Сам себя взорвал. Молодец!", "Audio/Banter/self-explosion-1" },
            { "Сам себя убил. Талант!", "Audio/Banter/self-kill-1" },
        });
    }
}
