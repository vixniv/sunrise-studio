# Blue Hifi v2 — Unity UI Export Manifest

Source file: `sunrise-projekku`

Figma file key: `HT5XPOnDsLTUEno0z1AayY`

Source section: `Blue Hifi v2` (`337:532`)

Reference canvas: `586 × 270`

## Export rules

- Never edit or detach the original Blue Hifi v2 components or instances.
- Export from separate plain-frame copies.
- Remove dynamic labels, quantities, quest text, dialogue text, time text, and costs from the copies.
- Exception: title-screen Play and Config labels are baked into their four button-state sprites.
- Keep icons, stitching, tape, paper edges, shadows, and decorative marks.
- PNG with transparency; export every HUD/UI asset at 4×. Keep raster backgrounds at their existing scale.
- Wrap every HUD/UI export in its own no-fill frame, with `8 px` transparent padding on every side.
- Wrapper names may use `_outerframe` in Figma; exported PNG names must exactly match existing Unity filenames.
- Do not apply the UI padding wrapper to title/gameplay backgrounds.
- Dynamic text is rebuilt with TextMeshPro in Unity; title Play/Config labels are rasterized in their button sprites.
- Stretchable paper/button panels use Unity `Image.Type = Sliced`; set borders from the visible corner and edge thickness.

## Required assets

| Unity path | Figma node | Scale | Purpose |
| --- | --- | ---: | --- |
| `Buttons/BTN_Title_Play_Default.png` | `347:629` | 4× | Title Play default |
| `Buttons/BTN_Title_Play_Pressed.png` | `347:642` | 4× | Title Play pressed |
| `Buttons/BTN_Title_Config_Default.png` | `347:643` | 4× | Title Config default |
| `Buttons/BTN_Title_Config_Pressed.png` | `347:648` | 4× | Title Config pressed |
| `Buttons/BTN_Pause_Default.png` | `337:549` | 4× | Gameplay pause default |
| `Buttons/BTN_Pause_Pressed.png` | `337:550` | 4× | Gameplay pause pressed |
| `Buttons/Pause_button_scrapbook.png` | local Figma component | 4× | Scrapbook gameplay pause default; complete paper, stitching, shadow, and pause glyph |
| `Buttons/Pause_button_scrapbook_pressed.png` | `379:848` | 4× | Scrapbook gameplay pause pressed; complete paper, stitching, shadow, and pause glyph |
| `Buttons/BTN_Close_Outline_Default.png` | `337:552` | 4× | Outline close/back default |
| `Buttons/BTN_Close_Outline_Pressed.png` | `337:558` | 4× | Outline close/back pressed |
| `Buttons/BTN_Close_Filled_Default.png` | `337:564` | 4× | Filled close/back default |
| `Buttons/BTN_Close_Filled_Pressed.png` | `337:570` | 4× | Filled close/back pressed |
| `Journal/TAB_Journal_Default.png` | `337:534` | 4× | Journal tab default |
| `Journal/TAB_Journal_Selected.png` | `337:539` | 4× | Journal tab selected |
| `Journal/TAB_Journal_Pressed.png` | `337:544` | 4× | Journal tab pressed |
| `Dialogue/PNL_Dialogue_Bubble.png` | `347:659` | 4× | Dialogue bubble skin |
| `Dialogue/BTN_Dialogue_Choice_Default.png` | `347:681` | 4× | Dialogue choice default |
| `Dialogue/BTN_Dialogue_Choice_Selected.png` | `347:682` | 4× | Dialogue choice selected |
| `Journal/STRIP_Pause_Default.png` | `337:588` | 4× | Pause-list strip default |
| `Journal/STRIP_Pause_Selected.png` | `337:592` | 4× | Pause-list strip selected |
| `Controls/TGL_Haptics_Off.png` | `337:700` | 4× | Checkbox off |
| `Controls/Checkbox_paper_tru.png` | `337:701` | 4× | Checkbox on; inner `V` baked into sprite |
| `Controls/Checkbox_paper_pressed.png` | `377:839` | 4× | Pressed checkbox; navy paper and white stitching |
| `Controls/Slider_white.png` | `267:529` | 4× | White slider track only; no blue fill or handle |
| `Controls/Slider_scroller_handle.png` | `267:528` | 4× | Separate movable slider handle |
| `Buttons/BTN_Interact_Default.png` | `337:741` | 4× | Interact default |
| `Buttons/BTN_Interact_Pressed.png` | `337:742` | 4× | Interact pressed |
| `Buttons/BTN_on_screen.png` | local Figma component | 4× | On-screen action button default |
| `Buttons/BTN_on_screen_pressed.png` | local Figma component | 4× | On-screen action button pressed |
| `Buttons/MAP_enter_button.png` | local Figma component | 4× | Map enter button default |
| `Buttons/MAP_enter_button_pressed.png` | local Figma component | 4× | Map enter button pressed |
| `Carousel/CARD_Action_Default.png` | `337:743` | 4× | Main carousel card |
| `Carousel/CARD_Action_Selected.png` | `337:744` | 4× | Main carousel selected card |
| `Carousel/CARD_Action_Small_Default.png` | `337:753` | 4× | Adjacent carousel card |
| `Carousel/CARD_Action_Small_Selected.png` | `337:752` | 4× | Adjacent selected card |
| `Carousel/PNL_Action_Description.png` | `337:754` | 4× | Action description paper |
| `Journal/STICKY_TimeLoop.png` | `337:599` | 4× | Loop/time sticky skin |
| `Journal/STICKY_Evening.png` | `337:600` | 4× | Evening sticky; accent `#B28DFF` |
| `Journal/STICKY_Afternoon.png` | `337:601` | 4× | Afternoon sticky |
| `Decorations/DEC_Dialogue_Scrim.png` | `337:657` | 4× | Dialogue focus scrim |
| `Decorations/DEC_Pause_Scrim.png` | `337:706` | 4× | Pause dimmer |
| `Decorations/DEC_Halftone.png` | `337:707` | 4× | Blue halftone overlay |
| `Map/MAP_School_Base.png` | `337:625` | 4× | Static school-map art; player and quest markers stay separate |
| `Map/AninMap.png` | local Figma component | 4× | Player map marker |
| `Map/MAP_door.png` | local Figma component | 4× | Map door marker |
| `Backgrounds/BG_Title_DayNight.png` | `337:602` image fill | 1× | Title background only; no title/buttons |
| `Decorations/TXT_Game_Title.png` | `365:733` | 4× | Rasterized game-title lettering with transparent background |
| `Dialogue/PNL_Dialogue_Body_9Slice.png` | `468:612` outer frame | 4× | Stretchable dialogue body |
| `Dialogue/TAG_Speaker_9slice.png` | local Figma component | 4× | Stretchable speaker tag |
| `Dialogue/DEC_Dialogue_NextIndicator.png` | local Figma component | 4× | Dialogue continue indicator |
| `Dialogue/DEC_Dialogue_Tail.png` | local Figma component | 4× | Dialogue bubble tail |

## Frame validation from 2026-11-04

| Screen | Actual size | Result |
| --- | ---: | --- |
| Title | `586 × 270` | Pass |
| Config | `586 × 270` | Pass |
| Explore + Interact | `586 × 270` | Pass |
| Actions + Inventory | `586 × 270` | Pass |
| Journal Map | `586 × 270` | Pass |
| Dialogue | `586 × 380` | Needs crop/layout correction to `586 × 270` |
| Pause | `586 × 268` | Needs height correction to `270` |
| Quest Log | `586 × 270` | Pass; nice-to-have, not MVP |

## Unity importer defaults

- Texture Type: `Sprite (2D and UI)`
- Sprite Mode: `Single`
- Mesh Type: `Full Rect`
- Compression: `None`
- Generate Mip Maps: off
- Alpha Is Transparency: on
- Filter Mode: `Bilinear` for paper/vector skins; `Point` for pixel-art backgrounds/icons
- Pixels Per Unit: `100` unless the scene’s established sprite scale requires another value
- Max Size: at least the exported image’s largest dimension

## Status

Export completed on 2026-11-04.

HUD/UI padding refresh completed on 2026-11-17.

- Applied the equivalent of `4 px` Figma-frame padding to 43 individual HUD/UI PNGs (`8 px` per side at 2×; `4 px` per side at 1×).
- Kept wrapper canvases transparent with no fill.
- Kept filenames identical to their component names.
- Excluded `Backgrounds/`; both title-background PNGs are byte-for-byte unchanged.
- Re-exported all 53 HUD/UI PNGs at 4× on 2026-11-17, following `Pause_button_scrapbook_outerframe` and `PNL_Dialogue_Body_9Slice_outerframe`: transparent outer frame, `8 px` design padding, no fill.
- Preserved every existing Unity filename and folder path. No `_outerframe` suffix added to output files.

- Created the separate Figma section `Unity Asset Exports — Blue Hifi v2`.
- Exported and verified 37 PNG assets.
- Removed dynamic text from component skins; the map keeps its static room labels.
- Re-exported four title buttons on 2026-11-05 with Play/Config labels baked into the PNGs.
- Split slider into separate track and handle PNGs on 2026-11-05.
- Exported white-only slider track on 2026-11-05; blue fill excluded.
- Re-exported `Checkbox_paper_tru` on 2026-11-05 with its inner `V` included.
- Exported `Checkbox_paper_pressed` on 2026-11-05.
- Exported `Pause_button_scrapbook` and `Pause_button_scrapbook_pressed` at `62 × 48` on 2026-11-05.
- Sorted assets into the Unity folders listed above.
- Preserved every original component and instance in `Blue Hifi v2`.
- Verified every PNG has readable pixel dimensions and non-zero content.
