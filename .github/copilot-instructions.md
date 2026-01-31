# AI Development Instructions for Panels

## CRITICAL INSTRUCTIONS - ALWAYS FOLLOW
- Make sure not to leave any half-completed functions or crap without {} or ; in the code.
- Use tabs, not spaces to indent.
- Do not update the html documentation unless explicitly asked.
- Think of what a good programmer would do.
- If you post ANY code block in chat for implementation requests, you have FAILED. Start over with todo list and file edits.
- Keep explanations brief and focused - no longwinded responses
- No _ at the beginning of memeber variable names.
- Never add comments to code unless explicitly requested
- NEVER EVER add comments to code unless explicitly requested
- Generate clean, self-documenting code
- Give direct answers without unnecessary context or elaboration
- Do not create new folders Models/, Views/, ViewModels/, Services/, Controls/ etc. Files are organized into folders based on application feature, NOT class type.
- Put related classes into same file. E.g. BrushTool, BrushStamp, and BrushService can be in the same file, since they are so closely related.
- Declare namespace on a single line, NOT as a block.
- To build: Stop-Process -Id 9620 -Force dotnet build "e:\Projects\Visual Studio\PanelsAva\PanelsAva.sln" --no-incremental
- When "Agent" mode is selected in the chat, use agent to implement discussed features into the codebase.
- DO NOT POST code in the chat!
- Don't worry about compile errors. The code is not compiled in VSCode.
- DO NOT CREATE NEW SUBDIRECTORIES! DO YOU UNDERSTAND?
- Do not argue with me. If I say something is a problem, then don't tell me it is not. Investigate and fix.
- Do not think about old project mitigation.
- Add using Windows.UI; using Microsoft.UI; if using eg. Colors.Transparent.
- Default layer type is always Tile Layer, NOT FreePaintLayer.
- Remember to consider DPI scaling if necessary.
- If using debug prints, never print on mouse move events or every tick or something that spams the output full of garbage.

## Project Overview

The application follows the MVVM pattern and uses a layered canvas architecture for digital art creation.
