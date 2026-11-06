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
- PNG with transparency; use 2× for smooth UI skins and 1× for pixel-art/raster backgrounds.
- Dynamic text is rebuilt with TextMeshPro in Unity; title Play/Config labels are rasterized in their button sprites.
- Stretchable paper/button panels use Unity `Image.Type = Sliced`; set borders from the visible corner and edge thickness.

## Required assets

| Unity path | Figma node | Scale | Purpose |
| --- | --- | ---: | --- |
| `Buttons/BTN_Title_Play_Default.png` | `347:629` | 2× | Title Play default |
| `Buttons/BTN_Title_Play_Pressed.png` | `347:642` | 2× | Title Play pressed |
| `Buttons/BTN_Title_Config_Default.png` | `347:643` | 2× | Title Config default |
| `Buttons/BTN_Title_Config_Pressed.png` | `347:648` | 2× | Title Config pressed |
| `Buttons/BTN_Pause_Default.png` | `337:549` | 2× | Gameplay pause default |
| `Buttons/BTN_Pause_Pressed.png` | `337:550` | 2× | Gameplay pause pressed |
| `Buttons/Pause_button_scrapbook.png` | local Figma component | 2× | Scrapbook gameplay pause default; complete paper, stitching, shadow, and pause glyph |
| `Buttons/Pause_button_scrapbook_pressed.png` | `379:848` | 2× | Scrapbook gameplay pause pressed; complete paper, stitching, shadow, and pause glyph |
| `Buttons/BTN_Close_Outline_Default.png` | `337:552` | 2× | Outline close/back default |
| `Buttons/BTN_Close_Outline_Pressed.png` | `337:558` | 2× | Outline close/back pressed |
| `Buttons/BTN_Close_Filled_Default.png` | `337:564` | 2× | Filled close/back default |
| `Buttons/BTN_Close_Filled_Pressed.png` | `337:570` | 2× | Filled close/back pressed |
| `Journal/TAB_Journal_Default.png` | `337:534` | 2× | Journal tab default |
| `Journal/TAB_Journal_Selected.png` | `337:539` | 2× | Journal tab selected |
| `Journal/TAB_Journal_Pressed.png` | `337:544` | 2× | Journal tab pressed |
| `Dialogue/PNL_Dialogue_Bubble.png` | `347:659` | 2× | Dialogue bubble skin |
| `Dialogue/BTN_Dialogue_Choice_Default.png` | `347:681` | 2× | Dialogue choice default |
| `Dialogue/BTN_Dialogue_Choice_Selected.png` | `347:682` | 2× | Dialogue choice selected |
| `Journal/STRIP_Pause_Default.png` | `337:588` | 2× | Pause-list strip default |
| `Journal/STRIP_Pause_Selected.png` | `337:592` | 2× | Pause-list strip selected |
| `Controls/TGL_Haptics_Off.png` | `337:700` | 2× | Checkbox off |
| `Controls/Checkbox_paper_tru.png` | `337:701` | 2× | Checkbox on; inner `V` baked into sprite |
| `Controls/Checkbox_paper_pressed.png` | `377:839` | 2× | Pressed checkbox; navy paper and white stitching |
| `Controls/Slider_white.png` | `267:529` | 2× | White slider track only; no blue fill or handle |
| `Controls/Slider_scroller_handle.png` | `267:528` | 2× | Separate movable slider handle |
| `Buttons/BTN_Interact_Default.png` | `337:741` | 2× | Interact default |
| `Buttons/BTN_Interact_Pressed.png` | `337:742` | 2× | Interact pressed |
| `Carousel/CARD_Action_Default.png` | `337:743` | 2× | Main carousel card |
| `Carousel/CARD_Action_Selected.png` | `337:744` | 2× | Main carousel selected card |
| `Carousel/CARD_Action_Small_Default.png` | `337:753` | 2× | Adjacent carousel card |
| `Carousel/CARD_Action_Small_Selected.png` | `337:752` | 2× | Adjacent selected card |
| `Carousel/PNL_Action_Description.png` | `337:754` | 2× | Action description paper |
| `Journal/STICKY_TimeLoop.png` | `337:599` | 2× | Loop/time sticky skin |
| `Journal/STICKY_Evening.png` | `337:600` | 2× | Evening sticky; accent `#B28DFF` |
| `Journal/STICKY_Afternoon.png` | `337:601` | 2× | Afternoon sticky |
| `Decorations/DEC_Dialogue_Scrim.png` | `337:657` | 2× | Dialogue focus scrim |
| `Decorations/DEC_Pause_Scrim.png` | `337:706` | 1× | Pause dimmer |
| `Decorations/DEC_Halftone.png` | `337:707` | 1× | Blue halftone overlay |
| `Map/MAP_School_Base.png` | `337:625` | 2× | Static school-map art; player and quest markers stay separate |
| `Backgrounds/BG_Title_DayNight.png` | `337:602` image fill | 1× | Title background only; no title/buttons |
| `Decorations/TXT_Game_Title.png` | `365:733` | 1× | Rasterized game-title lettering with transparent background |

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
