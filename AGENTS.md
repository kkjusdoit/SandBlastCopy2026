# Project Agent Guidelines

## Runtime UI Text

- Whenever UI copy becomes longer or gains extra lines, update its `RectTransform` height and the surrounding layout in the same change.
- For dynamic or multi-line TMP labels, explicitly enable word wrapping, use a non-truncating overflow mode, and configure auto sizing with a readable minimum font size.
- Do not assume that assigning a string means it will be visible. Verify the label's width, height, anchors, alignment, wrapping, overflow mode, and font glyph coverage.
- Every production-copy change must be checked against both `Assets/Resources/Fonts/NotoSansSC-FlowSand.ttf` and the baked TMP SDF character table. This project uses a subset font, so a missing glyph must be added to the source subset and the SDF rebuilt, or the copy must use already-supported characters.
- Rebalance or enlarge the containing panel when labels need more vertical space. Keep stable gaps between the title, body text, instructions, and buttons.
- Check the longest production copy at the 1080x1920 reference resolution and on narrow/tall mobile layouts before considering the UI complete.
