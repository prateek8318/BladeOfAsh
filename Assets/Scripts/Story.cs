// The original story of Blade of Ash: prologue, one chapter per realm, and the ending.

public class StoryPage
{
    public string header, body;
    public bool showMap;
    public StoryPage(string header, string body, bool showMap = false) { this.header = header; this.body = body; this.showMap = showMap; }
}

public static class Story
{
    public const string WorldName = "ASHENREACH";

    // Per-hero "why I fight" lines, used in the prologue and the ending.
    static readonly string[] Oath =
    {
        "Kael was the last sworn blade of Emberhold. He watched his king burn on the throne he was sworn to guard,\nand he walked out of the fire carrying only his sword and a promise.",
        "Ysra was a storm-warden, exiled for listening to the sky. When the Flame died, the sky fell silent —\nand Ysra stole the last of its voice to bring it back.",
        "Gorrun was a forge-giant of the deep mountain. When the Flame died his forges went cold,\nso he tore out the mountain's burning heart and carried it into the world.",
    };

    static readonly string[] Epilogue =
    {
        "Kael laid his sword across the steps of the empty throne. \"Your kingdom stands, my king,\" he said.\nThen, for the first time since the burning, he slept without dreaming of fire.",
        "The sky spoke again — thunder, rain, and wind over green fields. Ysra gave its voice back,\nand the storm-wardens who had banished her came down from the mountains to kneel.",
        "Gorrun set the mountain's heart beside the Flame, and the forges of Emberhold roared to life.\nThe first thing he forged was a bell, so the whole realm would know: the long night was over.",
    };

    public static StoryPage[] Prologue(int hero) => new[]
    {
        new StoryPage("BEFORE THE ASH",
            "For a thousand years the Undying Flame burned at the heart of Emberhold,\nand the kingdom of " + WorldName + " was warm, green and free."),
        new StoryPage("THE THREE TYRANTS",
            "Then three tyrants came out of the dark. They broke the Flame into three Embers\nand each carried one away to feed their own cursed realm.\n\nThe sky turned grey. The fields became ash. The dead stopped sleeping."),
        new StoryPage("A CHAMPION RISES", Oath[hero]),
        new StoryPage("THE OATH",
            "Three realms. Three tyrants. Three Embers.\nTake them back — and rekindle the Undying Flame.", true),
    };

    public static StoryPage ChapterIntro(int map)
    {
        switch (map)
        {
            case 1:
                return new StoryPage("CHAPTER II  ·  FROSTVEIL HOLLOW",
                    "North of the colosseum lies a grove that never thaws. Skaldra, the Rime Matriarch,\nfreezes the dead and raises them as her children.\n\nThe second Ember sleeps beneath her crown of ice.", true);
            case 2:
                return new StoryPage("CHAPTER III  ·  STORMSPIRE SUMMIT",
                    "At the edge of the world a mountain pierces the clouds. Ozrek, the Thunder Tyrant,\nclimbed it and tore a hole in the sky — the last Ember lights his endless storm.\n\nThere is nothing left between you and the Flame but him.", true);
            default:
                return new StoryPage("CHAPTER I  ·  CINDER COLOSSEUM",
                    "Emberhold's grand colosseum once held tournaments for the people. Vorthak, the Hollow King,\nturned it into a feeding pit and sits in its ashes with the first Ember burning in his chest.\n\nEnter the arena. Survive. Take back what he stole.", true);
        }
    }

    public static StoryPage ChapterOutro(int map)
    {
        switch (map)
        {
            case 1:
                return new StoryPage("THE SECOND EMBER",
                    "Skaldra's crown shatters and the grove begins, very slowly, to drip.\nThe second Ember is cold to the touch — until you hold it close. Then it remembers.\n\nAbove the northern peaks, lightning has not stopped for a hundred years.", true);
            case 2:
                return new StoryPage("THE LAST EMBER",
                    "Ozrek falls and the storm falls silent with him. In the sudden quiet you hear it —\nthree Embers, humming the same note.\n\nThey want to go home.", true);
            default:
                return new StoryPage("THE FIRST EMBER",
                    "The Hollow King crumbles into the ash he made. From his chest you pull the first Ember,\nsmall and fierce as a heartbeat.\n\nIts warmth pulls you north, toward a grove where the dead forget how to sleep.", true);
        }
    }

    public static StoryPage[] Ending(int hero) => new[]
    {
        new StoryPage("THE RETURN",
            "You carry the three Embers back across " + WorldName + " — through ash, through frost, through storm —\nto the cold altar at the heart of Emberhold.", true),
        new StoryPage("REKINDLING",
            "You set them together. For a moment, nothing.\nThen the Undying Flame draws a breath... and wakes."),
        new StoryPage("DAWN",
            "The grey sky cracks open and real sunlight falls for the first time in a hundred years.\nAsh becomes soil. Soil becomes green. The dead finally sleep."),
        new StoryPage("EPILOGUE", Epilogue[hero]),
        new StoryPage("BLADE OF ASH",
            "They will sing about you in every hall of Emberhold —\nthe one who walked through fire, frost and storm, and brought the morning back.\n\nTHE END"),
    };

    public static readonly string[] Credits =
    {
        "BLADE OF ASH",
        "",
        "AN ASHBORN STUDIO GAME",
        "",
        "— HEROES —",
        "Kael the Ashborn",
        "Ysra Stormveil",
        "Gorrun Cinderwall",
        "",
        "— TYRANTS —",
        "Vorthak, the Hollow King",
        "Skaldra, the Rime Matriarch",
        "Ozrek, the Thunder Tyrant",
        "",
        "— REALMS OF ASHENREACH —",
        "Cinder Colosseum",
        "Frostveil Hollow",
        "Stormspire Summit",
        "",
        "— MUSIC —",
        "\"Ashen Lament\"  ·  \"Ember March\"",
        "\"Tyrant's Crown\"  ·  \"Dawn Over Emberhold\"",
        "composed and synthesised in code",
        "",
        "Built entirely from code in Unity",
        "",
        "",
        "THANK YOU FOR PLAYING",
    };
}
