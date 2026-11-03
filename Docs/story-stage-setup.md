# Story stages: Editor setup (scenes 1–8)

> **Status (2026-11-01):** all of this is already set up in Dorm, LockerRoom, Classroom, Cafeteria,
> Auditorium, Library and SchoolMap. New hotspots are invisible graybox squares at placeholder
> positions, and new NPCs are graybox rectangles: move and resize them to match the art. Use this file
> as the reference for what each group holds.

The same rooms are reused by every story scene. The Yarn variable `$chapter_scene` (1–8) says which
story scene is playing. Each room keeps its art, and puts each story scene's objects in its own
group, which only shows during that story scene.

The last node of each story scene sets the next number, then `<<hard_reset>>`, `<<soft_reset>>` or
`<<end_gap>>` reloads the Dorm, so the Dorm shows the next group.

| `$chapter_scene` | Story scene | Starts in the Dorm at |
|---|---|---|
| 1 | Prologue (scene1.yarn) | morning |
| 2 | Gap 1 (scene2.yarn) | midnight |
| 3 | Day 2 (scene3.yarn) | morning |
| 4 | Gap 2 (scene4.yarn) | midnight |
| 5 | Day 3 (scene5.yarn) | morning |
| 6 | Gap 3 (scene6.yarn) | midnight |
| 7 | Day 4 (scene7.yarn) | morning |
| 8 | Gap 4 + ending (scene8.yarn) | midnight |

---

## 0. Building blocks

**Story group** (one per story scene, per room):
1. In the Hierarchy, right-click the scene root > **Create Empty**. Name it `Story_N` (such as `Story_3`).
2. **Add Component > Active If Yarn Variable**.
   - Variable: `$chapter_scene`
   - Value: `N` (such as `3`)
   - Targets: leave empty
   - Follow Changes: off
3. Keep `Story_N` itself **active** in the scene. It hides itself when the story is in another scene.

**Start node** (a node that plays when the room loads):
1. Inside the group, **Create Empty**, name it after the node (such as `Start_Day2_Start`).
2. **Add Component > Yarn Cutscene**.
   - Yarn Dialog: the scene's YarnDialogPresenter (on the Dialogue object)
   - Start Node: the node name
   - Player: the scene's Player (needed when the node has options)
   - Next Scene: **empty**
   - Play Once: on

**Interactable**: a ChoiceInteractable (or a copy of an existing one, such as Dorm_Bed) with one choice
whose **Yarn Node** is the node name.

**Condition child**: an empty child with **Active If Yarn Variable** for a flag, such as
`$gap1_key_returned` = `true`. Turn on **Follow Changes** when the flag changes while the player
is in the room (marked "live" below).

**Time child**: an empty child with **Active During Time** (Times such as `morning` or
`after_school`). Turn on **Follow Time Changes** when `<<advance_time>>` runs in that room.

**Actor**: a character that `<<npc_enter>>`/`<<npc_exit>>` (or `<<show>>`/`<<hide>>`) turns on and off.
1. Give it a **Dialog Speaker** whose Character Name matches the Yarn speaker exactly (such as `Fal`).
2. Make it **inactive** in the Hierarchy when it should walk in later.
3. On the Dialogue object's **Yarn Commands**, add an entry to **Actors**: Name = same name, Target = the object.

**Story door** (the Dorm door): a Door prefab instance inside the group. On its **Scene Door**, set
**Story Node** to that story scene's door node. The start nodes turn `$story_exit` on, so the door runs
the node instead of leaving freely.

---

## 1. Dorm

1. Move every existing story object (Dorm_Start Yarn Cutscene, Bed, Poster, Desk, the Door with
   Story Node `Dorm_Door`, the `Dorm_Calendar` object) into a new `Story_1`.
2. Create the other groups:

- `Story_2`
  - Start node `Gap1_Start`
  - Interactables: `Gap1_Dorm_Clock`, `Gap1_Dorm_KiranaBed`, `Gap1_Dorm_Window`
  - Story door: `Gap1_Dorm_Door`
  - Condition child `$gap1_key_returned` = `true`, holding start node `Gap1_Dorm_End`
  - Kirana's bed sprite: empty, blanket flat
- `Story_3`
  - Start node `Day2_Start`
  - Interactables: `Day2_Dorm_Bed`, `Day2_Dorm_Poster`, `Day2_Dorm_Desk`, `Day2_Dorm_KiranaBed`
  - Story door: `Day2_Dorm_Door`
  - Kirana asleep in her bed (a speaker named `Kirana`)
- `Story_4`
  - Start node `Gap2_Start`
  - Interactables: `Gap2_Dorm_Clock`, `Gap2_Dorm_KiranaBed`
  - Story door: `Gap2_Dorm_Door`
  - Condition child `$gap2_pole_done` = `true`, holding start node `Gap2_Dorm_End`
- `Story_5`
  - Time child `morning`: start node `Day3_Start`; interactables `Day2_Dorm_Bed`, `Day2_Dorm_Poster`,
    `Day2_Dorm_KiranaBed`, `Day3_Dorm_Blazer`; story door `Day3_Dorm_Door`
    (do **not** use `Day2_Dorm_Desk` or `Day2_Dorm_Door` here: they check Day 2's variables)
  - Time child `after_school`: start node `Day3_Dorm_Evening`; actor `Kirana` (inactive)
- `Story_6`
  - Start node `Gap3_Start`
  - Interactables: `Gap3_KiranaBed`, `Gap3_Calendar`
  - Story door: `Gap3_Door`
  - Condition child `$gap3_lila` = `true`, holding start node `Gap3_Dorm_End`
- `Story_7`
  - Time child `morning`: start node `Day4_Start`; interactable `Day4_Calendar`; actor `Kirana` (inactive)
  - Time child `after_school`: start node `Day4_Dorm_Night`
- `Story_8`
  - Condition child `$ending_started` = `false`: start node `Gap4_Start`; interactable
    `Gap4_Calendar`; story door `Gap4_Door`
  - Condition child `$ending_started` = `true`: start node `Ending_Dorm`

## 2. Locker Room

The Dialogue object's Yarn Commands already has **Scene After Hard Reset** = `Dorm`. Keep it.

1. Move the existing story objects (Lockers_Start, Lockers_Clock, Lockers_FalLocker, Lockers_KeyHook,
   Kirana/Dira/Lila) into `Story_1`.
2. Create:

- `Story_2`
  - Start node `Gap1_Lockers_Start`
  - Interactables: `Gap1_KeyHook` (rooftop key **on** the hook), `Gap1_JanitorCabinet`, `Gap1_FalLocker`
  - Condition child `$gap1_locker_open` = `true`, **Follow Changes on** (live), holding
    `Gap1_Photo`, `Gap1_ReportCard`, `Gap1_Letter`, `Gap1_Rocket`, `Gap1_SealedNote`, `Gap1_MathBook`
- `Story_3`
  - Start node `Day2_Lockers_Morning` (directly in `Story_3`, not in a time child)
  - Actor `Fal` (inactive), actors `Kirana`, `Dira`, `Lila` (inactive)
  - Time child `morning`, **Follow Time Changes on**: `Day2_Morning_FalLocker`,
    `Day2_Morning_KeyHook` (key **on** the hook), `Day2_Morning_Cabinet`
  - Time child `after_school`, **Follow Time Changes on**: `Day2_Eve_Clock`, `Day2_Eve_KeyHook`
    (hook **empty**)
  - Condition child `$d2_paper_dropped` = `true`, **Follow Changes on** (live): `Day2_CrumpledPaper`
  - No start node for `Day2_Lockers_Evening`: Day2_FalReads jumps to it.
- `Story_4`
  - Interactable `Gap2_KeyHook`
  - Condition child `$gap2_rooftop_key` = `true`, **Follow Changes on** (live): `Gap2_FalLocker`
    (only the interactable; keep the locker art outside the group)
- `Story_5`
  - Time child `after_school`: start node `Day3_Lockers`; actor `Fal` (inactive)
- `Story_6`
  - Interactables: `Gap3_LilaLocker`, `Gap3_LilaSketchbook`

## 3. Auditorium

- `Story_4`
  - Start node `Gap2_Auditorium_Enter`
  - Interactables: `Gap2_Hoop`, `Gap2_MicStand`, `Gap2_Flag`
  - Condition child `$gap2_seen_flag` = `true`, **Follow Changes on** (live): `Gap2_FlagpoleBase`
- `Story_5`
  - Time child `morning`: start node `Day3_Auditorium`; the students; interactables `Day3_Aud_Hoop`,
    `Day3_Aud_Stage`, `Day3_Aud_Flag`
  - Condition child `$d3_seen_flag` = `true`, **Follow Changes on** (live): `Day3_Aud_FlagpoleBase`
- `Story_8`
  - Condition child `$ending_started` = `false`: start node `Gap4_Auditorium`; actor `Night Anin`
    (inactive, Dialog Speaker `Night Anin`, Actors entry `Night Anin`)
  - Condition child `$ending_started` = `true`: start node `Ending_Auditorium`; Night Anin sitting on
    the stage edge (active, Dialog Speaker `Night Anin`)

## 4. Classroom

1. Move the existing story objects into `Story_1`.
2. Create:

- `Story_6`: interactable `Gap3_DiraBook`
- `Story_7`: start node `Day4_Classroom`; `Dira` and `Kirana` (speakers); actor `Lila` (inactive);
  interactable `Day4_Sketchbook`
- `Story_8`: start node `Ending_Classroom`; Kirana, Dira, Lila at the window table

## 5. Cafeteria

1. Move the existing story objects into `Story_1`.
2. `Story_7`
   - Time child `lunch`: start node `Day4_Lunch`; Dira, Lila, Kirana with Dialog Speakers and a
     ChoiceInteractable each, Yarn Node `Day4_Talk_Dira`, `Day4_Talk_Lila`, `Day4_Talk_Kirana`

## 6. Library (new setup)

1. Open `Library.unity`. Add the same shared prefabs as the other rooms: **Dialogue** (Dialogue Runner
   with a Persistent Variable Storage, Yarn Commands, Yarn Dialog Presenter), **Player**, **HUD**,
   **TransitionCanvas**, **InteractionCue**, and a **Door** back to `SchoolMap`.
2. `Story_5`: start node `Day3_Library`; Fal at the table (Dialog Speaker `Fal`, Actors entry `Fal`,
   active)
3. `Story_7`: start node `Day4_Library`; actor `Fal` (inactive)
4. **File > Build Profiles**: make sure `Library` is in the Scene List.

## 7. School Map

Add a door to the Library, the same way as the other buildings: a building with a **Map Door**
(Destination Scene `Library`, Building Collider, Entry Point). Day 4 needs it.

## 8. Speakers

- **Night Anin (Gap 3)**: the player talks as "Night Anin". In `Dorm`, `Classroom` and `LockerRoom`
  only, select the **Player**, **Add Component > Dialog Speaker**, Character Name `Night Anin`. This is a
  second Dialog Speaker next to the `Anin` one. Don't add it in the Auditorium, where Night Anin is
  someone else.
- **Note**: no speaker needed. Note lines show above the tapped object (the book), with a warning in
  the Console.

---

## Check it

1. Select `Assets/Dialogue/Dialogue.yarnproject`. The Inspector must show no errors.
2. Enter Play Mode and play through Scene 1. At the end of the Locker Room, the Console logs
   `Loop 1 ended; it is now midnight.`, the Dorm loads, and `Gap1_Start` plays ("...Again.").
3. Each story group logs a line like `Story_2: $chapter_scene is "2", needs "2"; showing.`
4. Commands that aren't built yet log `<<gap_mode True>> (not implemented yet)` and the dialogue
   continues.

**Shortcut for testing one story scene**: temporarily change
`<<declare $chapter_scene = 1>>` in scene1.yarn to the scene number, then start Play Mode from the Dorm.
Gap scenes expect midnight, so the HUD time label will say Morning in this shortcut. Change it back to
`1` afterwards.
