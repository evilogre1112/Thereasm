# Tutorial academy

The academy contains 22 playable lessons in four subjects. Controls are one continuous lesson; energy is another. Each special card has one lesson. Normal and Decay Snooper/Blind share their respective lessons, and Gambler presents two fixed opponent scenarios; the learner acts only as Village and can bet or pass. The academy has no quizzes or answer panels.

## Learning through actions

Instructions describe the next gesture. Consequences are discussed only after the real action. A deliberately rejected placement, retrieval or skill shakes/flashes the affected card, then keeps a short explanation visible until the player continues. Energy totals are described after spending, never before. Basic inspection is taught once in Controls. Card lessons allow optional right-click inspection without repeating mandatory inspect/close steps; progress pauses while the card is enlarged.

Perspective follows the actual acting faction and current voter. There are no manual viewpoint buttons. End turn performs the real normal-turn, Curse-to-Sacrifice or voter handoff; the guide detects both phase changes and changes between voters within Sacrifice. Authored checkpoints introduce a new situation, then its turns proceed through real EndTurn calls. Purge, Witch, Blind and Snooper let the learner observe a Cursermancer turn, then hand over to Village.

Gray cards are infected. This is taught after the gray rendering is visible on Cursermancer’s turn. Village sees the normal card rendering. There are no additional infection labels or colored status strips under cards.

The always-visible PlayerStageHUD identifies this screen’s faction/viewpoint and the current public stage/acting faction. It is saved in MainBoard and TutorialBoard. Cemetery and its centered grid occupy the board rectangle to the right of the lesson sidebar; its Close button remains reachable above the dimmer.

## Editing in Unity

Open Assets/UI/Tutorial/TutorialCourse.asset to edit subjects, approved Sprite icons and lesson references. MenuScenes contains saved TutorialLessonTile rows; update their direct exercise references when reordering. TutorialBoard’s TutorialWorkshop exposes previewExercise, Pass wager control and all guide references. Play the scene to preview that exercise directly. A menu selection takes priority.

The 22 referenced assets under Assets/UI/Tutorial/Lessons contain both English and Vietnamese copy, card sets, prepared commands and action beats. English card names remain unchanged. Setup clones CardData and skills so practice does not mutate the originals. Blind’s source, all card skill implementations, CardUI icon transforms, approved icons, audio and dialogue effects are preserved.

Tools/tutorial_curriculum_data.py builds the course through tutorial_curriculum_revision.py, tutorial_natural_turns.py and a final tutorial_context_copy.py editorial pass. The final pass runs after all scenario composers, so added turn-handoff steps cannot reintroduce unrelated energy/lock lectures. Tools/author_tutorial_revision.py updates only academy-owned row bindings and saved HUD/modal controls. The older unreferenced lesson assets are retained for compatibility but do not appear in the academy.

Onboarding still appears once per installation under thereasm.first-launch-answered. Completion is saved per lesson under thereasm.tutorial.done.<id>. Restart repeats the same lesson; completion/exit returns to the subject picker.

## Validation

Automated validation for academy lessons, wagers, and notebook handling is located in `Tests/Unity/TutorialAcademyValidation.cs`.

To run the academy validation suite:
```powershell
Tools/Run-TutorialValidation.ps1 -Suite Academy
```
