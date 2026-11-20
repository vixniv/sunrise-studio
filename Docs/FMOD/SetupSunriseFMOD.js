// Sunrise Studio — Automated FMOD Studio Setup Script (ES5 / QtScript)
// Creates Mixer Buses, Banks, Event Folders, and all 81 2D Events currently used in Unity,
// imports/links all audio files, configures looping music & multi-instrument SFX, saves, and builds banks.
//
// Usage in FMOD Studio:
//   Scripts -> Reload (if just installed)
//   Scripts -> Sunrise Studio -> Setup All Events & Banks

(function () {
    var UNITY_PROJECT_ROOT = "/Users/michelpierce/ProjectsnApps/Unity Project/sunrise-studio";

    var EVENT_DEFS = [
        // ==================== MUSIC (Looping 2D Timeline, Streaming) ====================
        { folder: "Music", name: "Main_Menu", bank: "Music", bus: "Music", volume: 1.0, loop: true, streaming: true, clips: ["Assets/Music/Sunrise_Main_Menu.wav"] },
        { folder: "Music", name: "Daylight", bank: "Music", bus: "Music", volume: 1.0, loop: true, streaming: true, clips: ["Assets/Music/Sunrise_Happy.wav"] },
        { folder: "Music", name: "Midnight", bank: "Music", bus: "Music", volume: 1.0, loop: true, streaming: true, clips: ["Assets/Music/Sunrise_midnight.wav"] },

        // ==================== DIALOGUE ====================
        {
            folder: "Dialogue", name: "Blip", bank: "UI", bus: "UI", volume: 0.55,
            clips: [
                "Assets/Sounds/UI/Dialogue/Dialogue T -2.wav",
                "Assets/Sounds/UI/Dialogue/Dialogue T -1.wav",
                "Assets/Sounds/UI/Dialogue/Dialogue T 0.wav"
            ]
        },
        { folder: "Dialogue", name: "Advance", bank: "UI", bus: "UI", volume: 0.8, clips: ["Assets/Sounds/UI/Dialogue/Dialogue Advance.wav"] },
        { folder: "Dialogue", name: "Choice", bank: "UI", bus: "UI", volume: 0.9, clips: ["Assets/Sounds/UI/Dialogue/Dialogue Choice.wav"] },

        // ==================== UI ====================
        { folder: "UI", name: "Click", bank: "UI", bus: "UI", volume: 0.8, clips: ["Assets/Sounds/UI/UI Click.wav"] },
        { folder: "UI", name: "Click_Short", bank: "UI", bus: "UI", volume: 0.8, clips: ["Assets/Sounds/UI/UI Click Short.wav"] },
        { folder: "UI", name: "Confirm", bank: "UI", bus: "UI", volume: 0.9, clips: ["Assets/Sounds/UI/UI Confirm .wav"] },
        { folder: "UI", name: "Error", bank: "UI", bus: "UI", volume: 0.9, clips: ["Assets/Sounds/UI/UI Error .wav"] },
        { folder: "UI", name: "Interact", bank: "UI", bus: "UI", volume: 0.85, clips: ["Assets/Sounds/UI/UI Interact.wav"] },
        {
            folder: "UI", name: "Item_Pickup", bank: "UI", bus: "UI", volume: 0.9,
            clips: [
                "Assets/Sounds/UI/UI Item Pickup.wav",
                "Assets/Sounds/UI/UI Item Pickup 2.wav"
            ]
        },
        { folder: "UI", name: "Quest_Popup", bank: "UI", bus: "UI", volume: 0.9, clips: ["Assets/Sounds/UI/UI Popup Quest .wav"] },
        { folder: "UI", name: "Quest_Complete", bank: "UI", bus: "UI", volume: 0.95, clips: ["Assets/Sounds/UI/UI Quest _ Objective Complete.wav"] },
        { folder: "UI", name: "Achievement", bank: "UI", bus: "UI", volume: 0.95, clips: ["Assets/Sounds/MISC/UI Achievement.wav"] },

        // ==================== SFX / INTERACT ====================
        { folder: "SFX", name: "Interact", bank: "SFX", bus: "SFX", volume: 1.0, clips: ["Assets/Sounds/interact.wav"] },

        // ==================== SFX / DOOR ====================
        { folder: "SFX/Door", name: "Open", bank: "SFX", bus: "SFX", volume: 0.9, clips: ["Assets/Sounds/Door/Door Open 1.wav"] },
        { folder: "SFX/Door", name: "Close", bank: "SFX", bus: "SFX", volume: 0.9, clips: ["Assets/Sounds/Door/Door Close 1.wav"] },
        { folder: "SFX/Door", name: "Generic", bank: "SFX", bus: "SFX", volume: 0.9, clips: ["Assets/Sounds/Door/door.wav"] },

        // ==================== SFX / LOCKER ====================
        { folder: "SFX/Locker", name: "Open", bank: "SFX", bus: "SFX", volume: 0.9, clips: ["Assets/Sounds/Locker/Locker Open.wav"] },
        { folder: "SFX/Locker", name: "Close", bank: "SFX", bus: "SFX", volume: 0.9, clips: ["Assets/Sounds/Locker/Locker Close.wav"] },
        { folder: "SFX/Locker", name: "Unlock", bank: "SFX", bus: "SFX", volume: 0.9, clips: ["Assets/Sounds/Scene 1 - 5/Locker Unlock.wav"] },
        { folder: "SFX/Locker", name: "Grab_Rattle", bank: "SFX", bus: "SFX", volume: 0.9, clips: ["Assets/Sounds/Scene 1 - 5/Locker Grab Rattle.wav"] },

        // ==================== SFX / TIME ====================
        { folder: "SFX/Time", name: "Day_Transition", bank: "SFX", bus: "SFX", volume: 0.9, clips: ["Assets/Sounds/Time System/Day State Transition.wav"] },
        { folder: "SFX/Time", name: "Night_Transition", bank: "SFX", bus: "SFX", volume: 0.9, clips: ["Assets/Sounds/Time System/Night State Transition.wav"] },
        { folder: "SFX/Time", name: "Soft_Reset", bank: "SFX", bus: "SFX", volume: 1.0, clips: ["Assets/Sounds/Time System/Soft Reset.wav"] },
        { folder: "SFX/Time", name: "Hard_Reset", bank: "SFX", bus: "SFX", volume: 1.0, clips: ["Assets/Sounds/Time System/Hard Reset 2.wav"] },
        { folder: "SFX/Time", name: "Clock_Ticking", bank: "SFX", bus: "SFX", volume: 0.75, loop: true, clips: ["Assets/Sounds/Scene 1 - 5/Clock Ticking.wav"] },
        { folder: "SFX/Time", name: "Alarm_Clock", bank: "SFX", bus: "SFX", volume: 0.9, clips: ["Assets/Sounds/Scene 1 - 5/Alarm Clock.wav"] },

        // ==================== SFX / FOOTSTEPS (Multi-Instrument + Variation) ====================
        {
            folder: "SFX/Footsteps", name: "Carpet", bank: "SFX", bus: "SFX", volume: 0.9, randomizeFootstep: true,
            clips: [
                "Assets/Sounds/Walk/Walk Carpet/Walk Carpet 1.wav",
                "Assets/Sounds/Walk/Walk Carpet/Walk Carpet 2.wav",
                "Assets/Sounds/Walk/Walk Carpet/Walk Carpet 3.wav",
                "Assets/Sounds/Walk/Walk Carpet/Walk Carpet 4.wav",
                "Assets/Sounds/Walk/Walk Carpet/Walk Carpet 5.wav",
                "Assets/Sounds/Walk/Walk Carpet/Walk Carpet 6.wav"
            ]
        },
        {
            folder: "SFX/Footsteps", name: "Cement", bank: "SFX", bus: "SFX", volume: 0.9, randomizeFootstep: true,
            clips: [
                "Assets/Sounds/Walk/Walk Cement Tiles/Walk CementTiles 1.wav",
                "Assets/Sounds/Walk/Walk Cement Tiles/Walk CementTiles 2.wav",
                "Assets/Sounds/Walk/Walk Cement Tiles/Walk CementTiles 3.wav",
                "Assets/Sounds/Walk/Walk Cement Tiles/Walk CementTiles 4.wav",
                "Assets/Sounds/Walk/Walk Cement Tiles/Walk CementTiles 5.wav",
                "Assets/Sounds/Walk/Walk Cement Tiles/Walk CementTiles 6.wav"
            ]
        },
        {
            folder: "SFX/Footsteps", name: "Concrete", bank: "SFX", bus: "SFX", volume: 0.9, randomizeFootstep: true,
            clips: [
                "Assets/Sounds/Walk/Walk Concrete/Walk Concrete 1.wav",
                "Assets/Sounds/Walk/Walk Concrete/Walk Concrete 2.wav",
                "Assets/Sounds/Walk/Walk Concrete/Walk Concrete 3.wav",
                "Assets/Sounds/Walk/Walk Concrete/Walk Concrete 4.wav",
                "Assets/Sounds/Walk/Walk Concrete/Walk Concrete 5.wav",
                "Assets/Sounds/Walk/Walk Concrete/Walk Concrete 6.wav"
            ]
        },
        {
            folder: "SFX/Footsteps", name: "Grass", bank: "SFX", bus: "SFX", volume: 0.9, randomizeFootstep: true,
            clips: [
                "Assets/Sounds/Walk/Walk Grass Outdoor/Footstep Grass 1.wav",
                "Assets/Sounds/Walk/Walk Grass Outdoor/Footstep Grass 2.wav",
                "Assets/Sounds/Walk/Walk Grass Outdoor/Footstep Grass 3.wav",
                "Assets/Sounds/Walk/Walk Grass Outdoor/Footstep Grass 4.wav",
                "Assets/Sounds/Walk/Walk Grass Outdoor/Footstep Grass 5.wav",
                "Assets/Sounds/Walk/Walk Grass Outdoor/Footstep Grass 6.wav"
            ]
        },
        {
            folder: "SFX/Footsteps", name: "Linoleum", bank: "SFX", bus: "SFX", volume: 0.9, randomizeFootstep: true,
            clips: [
                "Assets/Sounds/Walk/Walk Hall Echo _ Reverb/Walk Linoleum ( Auditorium ) 1.wav",
                "Assets/Sounds/Walk/Walk Hall Echo _ Reverb/Walk Linoleum ( Auditorium ) 2.wav",
                "Assets/Sounds/Walk/Walk Hall Echo _ Reverb/Walk Linoleum ( Auditorium ) 3.wav",
                "Assets/Sounds/Walk/Walk Hall Echo _ Reverb/Walk Linoleum ( Auditorium ) 4.wav",
                "Assets/Sounds/Walk/Walk Hall Echo _ Reverb/Walk Linoleum ( Auditorium ) 5.wav",
                "Assets/Sounds/Walk/Walk Hall Echo _ Reverb/Walk Linoleum ( Auditorium ) 6.wav"
            ]
        },
        {
            folder: "SFX/Footsteps", name: "Wood", bank: "SFX", bus: "SFX", volume: 0.9, randomizeFootstep: true,
            clips: [
                "Assets/Sounds/Walk/Wood/wood-1.wav",
                "Assets/Sounds/Walk/Wood/wood-2.wav",
                "Assets/Sounds/Walk/Wood/wood-3.wav",
                "Assets/Sounds/Walk/Wood/wood-4.wav",
                "Assets/Sounds/Walk/Wood/wood-5.wav",
                "Assets/Sounds/Walk/Wood/wood-6.wav"
            ]
        },

        // ==================== SFX / STORY / SCENE 1-5 ====================
        { folder: "SFX/Story/Scene1_5", name: "Fall_Impact", bank: "SFX", bus: "SFX", volume: 1.0, clips: ["Assets/Sounds/Scene 1 - 5/Fall Impact.wav"] },
        { folder: "SFX/Story/Scene1_5", name: "Distant_Scream", bank: "SFX", bus: "SFX", volume: 0.9, clips: ["Assets/Sounds/Scene 1 - 5/Distant Scream.wav"] },
        {
            folder: "SFX/Story/Scene1_5", name: "Running_To_Fal", bank: "SFX", bus: "SFX", volume: 0.9,
            clips: [
                "Assets/Sounds/Scene 1 - 5/Running to Fal.wav",
                "Assets/Sounds/Scene 1 - 5/Running to Fal(1).wav"
            ]
        },
        { folder: "SFX/Story/Scene1_5", name: "Wardrobe_Open", bank: "SFX", bus: "SFX", volume: 0.9, clips: ["Assets/Sounds/Scene 1 - 5/Open Wardrobe.wav"] },
        { folder: "SFX/Story/Scene1_5", name: "Wardrobe_Close", bank: "SFX", bus: "SFX", volume: 0.9, clips: ["Assets/Sounds/Scene 1 - 5/Close Wardrobe.wav"] },
        { folder: "SFX/Story/Scene1_5", name: "Clothes_Rustle", bank: "SFX", bus: "SFX", volume: 0.85, clips: ["Assets/Sounds/Scene 1 - 5/Changing Uniform.wav"] },
        {
            folder: "SFX/Story/Scene1_5", name: "Chair_Move", bank: "SFX", bus: "SFX", volume: 0.85,
            clips: [
                "Assets/Sounds/Scene 1 - 5/Chair Movement 1.wav",
                "Assets/Sounds/Scene 1 - 5/Chair Movement 2.wav"
            ]
        },
        { folder: "SFX/Story/Scene1_5", name: "Food_Serving", bank: "SFX", bus: "SFX", volume: 0.9, clips: ["Assets/Sounds/Scene 1 - 5/Plate Serving.wav"] },
        { folder: "SFX/Story/Scene1_5", name: "Plate_Put_Down", bank: "SFX", bus: "SFX", volume: 0.9, clips: ["Assets/Sounds/Scene 1 - 5/Plate Put Down.wav"] },
        { folder: "SFX/Story/Scene1_5", name: "Ambience_Drop_After_Fal", bank: "SFX", bus: "SFX", volume: 1.0, clips: ["Assets/Sounds/Scene 1 - 5/Ambience Drop After Fal .wav"] },
        { folder: "SFX/Story/Scene1_5", name: "Sting_Before_Again", bank: "SFX", bus: "SFX", volume: 1.0, clips: ["Assets/Sounds/Scene 1 - 5/Sting Before _Again..._.wav"] },
        {
            folder: "SFX/Story/Scene1_5", name: "Janitor_Cabinet_Open", bank: "SFX", bus: "SFX", volume: 0.9,
            clips: [
                "Assets/Sounds/Scene 1 - 5/Janitor Cabinet With Tail.wav",
                "Assets/Sounds/Scene 1 - 5/Janitor Cabinet Without Tail.wav"
            ]
        },
        { folder: "SFX/Story/Scene1_5", name: "Keys_Pickup", bank: "SFX", bus: "SFX", volume: 0.9, clips: ["Assets/Sounds/Scene 1 - 5/Keys Pickup.wav"] },
        { folder: "SFX/Story/Scene1_5", name: "Keys_Return", bank: "SFX", bus: "SFX", volume: 0.9, clips: ["Assets/Sounds/Scene 1 - 5/Keys Return.wav"] },
        { folder: "SFX/Story/Scene1_5", name: "Paper_Pickup", bank: "SFX", bus: "SFX", volume: 0.9, clips: ["Assets/Sounds/Scene 1 - 5/Paper Pickup.wav"] },
        { folder: "SFX/Story/Scene1_5", name: "Paper_Handle", bank: "SFX", bus: "SFX", volume: 0.8, clips: ["Assets/Sounds/Scene 1 - 5/Paper Handle.wav"] },
        {
            folder: "SFX/Story/Scene1_5", name: "Paper_Placement", bank: "SFX", bus: "SFX", volume: 0.85,
            clips: [
                "Assets/Sounds/Scene 1 - 5/Paper Placement.wav",
                "Assets/Sounds/Scene 1 - 5/Paper Placement on Table.wav"
            ]
        },
        { folder: "SFX/Story/Scene1_5", name: "Writing_Pencil", bank: "SFX", bus: "SFX", volume: 0.75, clips: ["Assets/Sounds/Scene 1 - 5/Writing Pencil.wav"] },
        { folder: "SFX/Story/Scene1_5", name: "Book_Pickup", bank: "SFX", bus: "SFX", volume: 0.9, clips: ["Assets/Sounds/Scene 1 - 5/Book Pickup.wav"] },
        { folder: "SFX/Story/Scene1_5", name: "Book_Open", bank: "SFX", bus: "SFX", volume: 0.85, clips: ["Assets/Sounds/Scene 1 - 5/Book Open.wav"] },
        { folder: "SFX/Story/Scene1_5", name: "Book_Page_Turn", bank: "SFX", bus: "SFX", volume: 0.8, clips: ["Assets/Sounds/Scene 1 - 5/Book Flip.wav"] },
        { folder: "SFX/Story/Scene1_5", name: "Paper_Drop", bank: "SFX", bus: "SFX", volume: 0.9, clips: ["Assets/Sounds/Scene 1 - 5/Paper Drop.wav"] },
        { folder: "SFX/Story/Scene1_5", name: "Books_Drop", bank: "SFX", bus: "SFX", volume: 1.0, clips: ["Assets/Sounds/Scene 1 - 5/Book Drop.wav"] },
        { folder: "SFX/Story/Scene1_5", name: "Crumpled_Note_Unfold", bank: "SFX", bus: "SFX", volume: 0.85, clips: ["Assets/Sounds/Scene 1 - 5/Crumpled Note Unfold.wav"] },
        { folder: "SFX/Story/Scene1_5", name: "Blazer_Pocket", bank: "SFX", bus: "SFX", volume: 0.8, clips: ["Assets/Sounds/Scene 1 - 5/Blazer Pocket.wav"] },
        { folder: "SFX/Story/Scene1_5", name: "Bolt_Unscrew", bank: "SFX", bus: "SFX", volume: 0.9, clips: ["Assets/Sounds/Scene 1 - 5/Bolt Unscrew.wav"] },
        { folder: "SFX/Story/Scene1_5", name: "Flag_Cloth", bank: "SFX", bus: "SFX", volume: 0.85, clips: ["Assets/Sounds/Scene 1 - 5/Flag Cloth Movement.wav"] },
        { folder: "SFX/Story/Scene1_5", name: "Bolt_Tighten", bank: "SFX", bus: "SFX", volume: 0.9, clips: ["Assets/Sounds/Scene 1 - 5/Bolt Tighten.wav"] },
        { folder: "SFX/Story/Scene1_5", name: "Memory_Transition_In", bank: "SFX", bus: "SFX", volume: 1.0, clips: ["Assets/Sounds/Scene 1 - 5/Memory Transition In.wav"] },
        { folder: "SFX/Story/Scene1_5", name: "Memory_Transition_Back", bank: "SFX", bus: "SFX", volume: 1.0, clips: ["Assets/Sounds/Scene 1 - 5/Memory Transition Back.wav"] },
        { folder: "SFX/Story/Scene1_5", name: "Basketball_Practice", bank: "SFX", bus: "SFX", volume: 0.8, clips: ["Assets/Sounds/Scene 1 - 5/Basketball Practice.wav"] },
        { folder: "SFX/Story/Scene1_5", name: "Bed_Drop", bank: "SFX", bus: "SFX", volume: 0.9, clips: ["Assets/Sounds/Scene 1 - 5/Bed Drop.wav"] },

        // ==================== SFX / STORY / SCENE 6-8 ====================
        { folder: "SFX/Story/Scene6_8", name: "Calendar_Paper_Handle", bank: "SFX", bus: "SFX", volume: 0.85, clips: ["Assets/Sounds/Scene 6-8/Calendar Paper Handle.wav"] },
        { folder: "SFX/Story/Scene6_8", name: "Calendar_Writing", bank: "SFX", bus: "SFX", volume: 0.8, clips: ["Assets/Sounds/Scene 6-8/Calendar Writing.wav"] },
        { folder: "SFX/Story/Scene6_8", name: "Sketchbook_Page_Turn", bank: "SFX", bus: "SFX", volume: 0.85, clips: ["Assets/Sounds/Scene 6-8/Sketchbook Page Turn.wav"] },
        { folder: "SFX/Story/Scene6_8", name: "Pencil_Sketching", bank: "SFX", bus: "SFX", volume: 0.75, clips: ["Assets/Sounds/Scene 6-8/Pencil Sketching.wav"] },
        { folder: "SFX/Story/Scene6_8", name: "Light_Switch", bank: "SFX", bus: "SFX", volume: 0.9, clips: ["Assets/Sounds/Scene 6-8/Light Switch.wav"] },
        { folder: "SFX/Story/Scene6_8", name: "Drowsiness", bank: "SFX", bus: "SFX", volume: 0.9, clips: ["Assets/Sounds/Scene 6-8/Drowsiness Effect.wav"] },
        { folder: "SFX/Story/Scene6_8", name: "Snap_Awake", bank: "SFX", bus: "SFX", volume: 0.9, clips: ["Assets/Sounds/Scene 6-8/Snap Awake.wav"] },
        { folder: "SFX/Story/Scene6_8", name: "Reveal_Sting", bank: "SFX", bus: "SFX", volume: 1.0, clips: ["Assets/Sounds/Scene 6-8/Reveal Sting.wav"] },
        { folder: "SFX/Story/Scene6_8", name: "Gap_Transition", bank: "SFX", bus: "SFX", volume: 1.0, clips: ["Assets/Sounds/Scene 6-8/Gap Transition.wav"] },
        { folder: "SFX/Story/Scene6_8", name: "Night_Anin_Reveal", bank: "SFX", bus: "SFX", volume: 1.0, clips: ["Assets/Sounds/Scene 6-8/Night Anin Reveal Sting.wav"] },
        { folder: "SFX/Story/Scene6_8", name: "Festival_Transition", bank: "SFX", bus: "SFX", volume: 1.0, clips: ["Assets/Sounds/Scene 6-8/Festival Transition.wav"] },
        { folder: "SFX/Story/Scene6_8", name: "Festival_Crowd", bank: "SFX", bus: "SFX", volume: 0.75, clips: ["Assets/Sounds/Scene 6-8/Festival Crowd.wav"] },
        { folder: "SFX/Story/Scene6_8", name: "Separation_Transition", bank: "SFX", bus: "SFX", volume: 1.0, clips: ["Assets/Sounds/Scene 6-8/Separation Transition.wav"] },
        { folder: "SFX/Story/Scene6_8", name: "End_Chapter_Sting", bank: "SFX", bus: "SFX", volume: 1.0, clips: ["Assets/Sounds/Scene 6-8/End Chapter Sting.wav"] }
    ];

    function linearToDb(linear) {
        if (linear >= 0.999) return 0;
        var clamped = Math.max(0.001, linear);
        return 20 * (Math.log(clamped) / Math.LN10);
    }

    function findOrCreateBank(name) {
        var existing = studio.project.lookup("bank:/" + name);
        if (existing) return existing;

        var bank = studio.project.create("Bank");
        bank.name = name;
        bank.folder = studio.project.workspace.masterBankFolder;
        return bank;
    }

    function findOrCreateBus(name) {
        var existing = studio.project.lookup("bus:/" + name);
        if (existing) return existing;

        var bus = studio.project.create("MixerGroup");
        bus.name = name;
        bus.output = studio.project.workspace.mixer.masterBus;
        return bus;
    }

    function findChildFolder(parentFolder, folderName) {
        var items = parentFolder.items;
        for (var i = 0; i < items.length; i++) {
            if (items[i].isOfExactType("EventFolder") && items[i].name === folderName) {
                return items[i];
            }
        }
        return null;
    }

    function findOrCreateFolder(folderPath) {
        var parts = folderPath.split("/");
        var current = studio.project.workspace.masterEventFolder;
        for (var i = 0; i < parts.length; i++) {
            var part = parts[i];
            if (!part) continue;
            var child = findChildFolder(current, part);
            if (!child) {
                child = studio.project.create("EventFolder");
                child.name = part;
                child.folder = current;
            }
            current = child;
        }
        return current;
    }

    function getProjectDir() {
        var fp = studio.project.filePath || "";
        var idx = fp.lastIndexOf("/");
        return idx >= 0 ? fp.substring(0, idx) : "";
    }

    function findOrImportAudio(unityRelPath) {
        var fmodRelPath = unityRelPath.indexOf("Assets/") === 0
            ? unityRelPath.substring("Assets/".length)
            : unityRelPath;

        // 1. Check if already imported in FMOD Assets
        var masterAssets = studio.project.workspace.masterAssetFolder;
        try {
            var existing = masterAssets.getAsset(fmodRelPath);
            if (existing && existing.isOfExactType("AudioFile")) {
                return existing;
            }
        } catch (e) {}

        // 2. Try importing in-place from FMOD project's Assets folder if the file is already there
        var projectDir = getProjectDir();
        if (projectDir) {
            var inProjectPath = projectDir + "/Assets/" + fmodRelPath;
            var importedLocal = studio.project.importAudioFile(inProjectPath);
            if (importedLocal) return importedLocal;
        }

        // 3. Fallback: import directly from Unity project
        var unityAbsPath = UNITY_PROJECT_ROOT + "/" + unityRelPath;
        return studio.project.importAudioFile(unityAbsPath);
    }

    function runSetup() {
        var banks = {
            Master: findOrCreateBank("Master"),
            Music: findOrCreateBank("Music"),
            UI: findOrCreateBank("UI"),
            SFX: findOrCreateBank("SFX")
        };

        var buses = {
            Music: findOrCreateBus("Music"),
            UI: findOrCreateBus("UI"),
            SFX: findOrCreateBus("SFX")
        };

        // Ensure existing Button_Click is routed cleanly if present
        var buttonClick = studio.project.lookup("event:/UI/Button_Click");
        if (buttonClick) {
            try { buttonClick.mixerInput.output = buses.UI; } catch (e) {}
            try { buttonClick.relationships.banks.add(banks.UI); } catch (e) {}
        }

        var createdCount = 0;
        var updatedCount = 0;
        var missingClips = [];

        for (var i = 0; i < EVENT_DEFS.length; i++) {
            var def = EVENT_DEFS[i];
            var eventPath = "event:/" + def.folder + "/" + def.name;
            var existing = studio.project.lookup(eventPath);

            var evt;
            if (existing) {
                evt = existing;
                updatedCount++;
            } else {
                // 2D event (withSpatializer = false)
                evt = studio.project.workspace.addEvent(def.name, false);
                evt.folder = findOrCreateFolder(def.folder);
                createdCount++;
            }

            // Route to Bus and assign to Bank
            try { evt.mixerInput.output = buses[def.bus]; } catch (e) {}
            try { evt.relationships.banks.add(banks[def.bank]); } catch (e) {}

            // Only populate tracks if event doesn't already have audio tracks
            if (evt.groupTracks && evt.groupTracks.length > 0) {
                continue;
            }

            var audioFiles = [];
            for (var c = 0; c < def.clips.length; c++) {
                var af = findOrImportAudio(def.clips[c]);
                if (af) {
                    if (def.streaming) {
                        try { af.isStreaming = true; } catch (e) {}
                    }
                    audioFiles.push(af);
                } else {
                    missingClips.push(def.clips[c]);
                }
            }

            if (audioFiles.length === 0) {
                continue;
            }

            var track = evt.addGroupTrack("Audio 1");
            var volDb = linearToDb(def.volume);

            if (audioFiles.length === 1) {
                var afSingle = audioFiles[0];
                var len = (afSingle.length && afSingle.length > 0) ? afSingle.length : 1.0;
                var single = track.addSound(evt.timeline, "SingleSound", 0, len);
                single.audioFile = afSingle;
                single.volume = volDb;

                if (def.loop) {
                    var markerTrack = evt.addMarkerTrack();
                    markerTrack.addRegion(0, len, "Loop", studio.project.regionLoopMode.Looping);
                }
            } else {
                var maxLen = 0.5;
                for (var m = 0; m < audioFiles.length; m++) {
                    if (audioFiles[m].length && audioFiles[m].length > maxLen) {
                        maxLen = audioFiles[m].length;
                    }
                }
                var multi = track.addSound(evt.timeline, "MultiSound", 0, maxLen);
                multi.volume = volDb;

                for (var s = 0; s < audioFiles.length; s++) {
                    var sub = studio.project.create("SingleSound");
                    sub.audioFile = audioFiles[s];
                    sub.owner = multi;
                }

                if (def.randomizeFootstep) {
                    try {
                        var pitchMod = multi.addModulator("RandomizerModulator", "pitch");
                        if (pitchMod) pitchMod.amount = 2.0; // +/- ~1 semitone
                    } catch (e) {}
                    try {
                        var volMod = multi.addModulator("RandomizerModulator", "volume");
                        if (volMod) volMod.amount = 1.5; // slight natural volume variation
                    } catch (e) {}
                }
            }
        }

        var saved = studio.project.save();
        var built = studio.project.build();

        var summary = "Sunrise FMOD Setup Complete!\n\n" +
            "Created events: " + createdCount + "\n" +
            "Updated existing: " + updatedCount + "\n" +
            "Banks: Master, Music, UI, SFX\n" +
            "Buses: Music, UI, SFX\n" +
            "Saved: " + saved + " | Built: " + built;

        if (missingClips.length > 0) {
            summary += "\n\nMissing clips (" + missingClips.length + "):\n- " + missingClips.join("\n- ");
        }

        console.log(summary);
        studio.system.message(summary);
    }

    studio.menu.addMenuItem({
        name: "Sunrise Studio\\Setup All Events & Banks",
        execute: runSetup
    });
})();
