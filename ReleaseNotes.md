### 🚀 Features

- *(sdk)* Update sdk and dependencies to .net 11

### 🐛 Bug Fixes

- *(Reader)* Improve error handling for JS module loading and context retrieval
- *(ContextMenu)* Handle errors during initialization and positioning
- *(Pagination)* Improve error handling during component initialization
- *(ContextMenu)* Handle exceptions during disposal of module
- *(BookSection)* Handle initialization errors in OnAfterRenderAsync
- *(BookSection)* Handle exceptions during page scrolling and disposal
- *(TextSelectionHandler)* Add error handling for module initialization and selection actions
- *(ChapterTitle)* Add error handling for module initialization and title processing
- *(TableOfContentsEntry)* Handle exceptions during JS module import and scrolling
- *(Navigation)* Add error handling for initialization and layout updates
- *(MauiProgram)* Enable Snackbar on Windows and update app manifest for COM and desktop extensions
- *(AnnotationNoteDialog)* Wrap annotation text in a div for proper clamping

### 🚜 Refactor

- *(Setup)* Remove unnecessary StateHasChanged calls in async methods
- *(BookSection)* Improve layout structure and styling
- *(Navigation)* Remove unnecessary null check for _dotNetReference
- *(MainPage)* Remove unused HeadOutlet component which was messing with trimming
- *(Apps)* Load apps only when needed instead of on app startup
- *(AppDrawer)* Rename AppsService to AppDrawerService and update references

### ⚙️ Miscellaneous Tasks

- *(project)* Update project file to enable runtime async features
- *(project)* Update LangVersion to latest
- *(project)* Enable PublishReadyToRun for android target
- Update BookHeaven.Core to 2.2.0
- *(android-build)* Update .NET and Android version to 11.x
- Update BookHeaven.Core to 2.2.1
