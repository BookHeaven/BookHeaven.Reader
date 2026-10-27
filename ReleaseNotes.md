The reader now uses a new, custom-made engine for page-counting (inspired by CREngine, the engine powering KOReader).  
This engine basically parses and computes the HTML and CSS of the book and creates a "virtual DOM" to distribute the elements and calculate page counts, which is way faster than rendering in the real DOM and then counting using JavaScript.  
This makes it possible to count all chapters at once, which provides a completely accurate percentage progress (and eases my OCD).

The old counting mechanism is now gone, but I don't discard bringing it back for precision's sake, since the new engine may miscount in some edge cases (aka, css properties I haven't encountered), but so far it has been working well for all books I have tested it with different kinds of reader settings.

<hr />

## Improvements
- Images inside books are no longer loaded in base64, which results in lower memory usage.
- The books are now directly processed with the EbookReader and cached on disk when downloaded from the server, which decreases loading times.
- The page counts are cached as well, which also decreases loading times.
- The current page is adjusted if needed when changing settings that affect the page count.
- The progress will now be saved regularly instead of only when the book is closed, which prevents losing progress if the app crashes or is killed by the system.

## Fixes
- Fixed an unintended regression from last version that caused the suspended time to not be subtracted from the total reading time.



