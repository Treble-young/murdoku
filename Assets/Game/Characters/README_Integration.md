# Character System Integration

`CharacterPanelTest.unity` is an isolated development scene. Do not add it to the
normal game flow and do not copy its camera, canvas, event system, or test board
into a production level.

## Production board contract

Each production board cell that can receive a character must implement
`ICharacterPlacementCell`:

```csharp
public interface ICharacterPlacementCell
{
    Vector2Int GridPosition { get; }
    bool IsPlaceable { get; }
    bool IsOccupied { get; }

    bool TryPlaceCharacter(CharacterData character);
    void RemoveCharacter();
}
```

When a production cell is clicked, pass that cell to
`CharacterPlacementController.HandleCellClicked`. The placement controller must
not look up `Tile`, `GridManager`, or any test-board component.

For drag-and-drop input, resolve the dragged `CharacterData` and pass it together
with the target cell to
`CharacterPlacementController.HandleCharacterDropped(CharacterData, ICharacterPlacementCell)`.
The same method supports first placement and moving an already placed character;
an occupied target is rejected without clearing the source cell.

`TryPlaceCharacter` must return `false` without changing state when the target is
blocked, occupied, or cannot display the supplied character. `RemoveCharacter`
must clear the token and occupancy state for that cell.

## Scene setup

1. Instantiate `CharacterPanel.prefab` under the production Canvas.
2. Instantiate `CharacterSystem.prefab` once in the scene.
3. Assign the panel instance's `CharacterPanelView` to `CharacterPanelUI.View`.
4. Assign the desired `CharacterData` assets to `CharacterPanelUI.Characters`.
5. Forward production-cell click events to
   `CharacterPlacementController.HandleCellClicked(ICharacterPlacementCell)`.
6. Optionally forward production-cell drop events to
   `CharacterPlacementController.HandleCharacterDropped(CharacterData, ICharacterPlacementCell)`.
7. Keep exactly one EventSystem in the production scene.
8. Keep only the camera(s) used by the production scene. The character panel uses
   the existing Canvas and does not require its own camera.

Clicking a selected character card again clears the selection and restores its
`VisualRoot` to scale `1.0`. Beginning a drag selects the source card without
toggling it off.

The three data assets under `Data/` are placeholders for the test scene. Portraits,
names, gender, clue text, and placeholder colors can be replaced in the Inspector.

## Test-only assets

`TestBoardController`, `TestBoardCellUI`, `TestBoardCell.prefab`,
`CharacterPanelTestCoordinator`, and `CharacterPanelTest.unity` are test-only.
They demonstrate the placement interface but are not the production-board
implementation.

Do not add `CharacterPanelTest.unity` to Build Settings or load it from the main
menu. Do not move its EventSystem, Main Camera, or Canvas into `Level01.unity`.
