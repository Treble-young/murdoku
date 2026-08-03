using UnityEngine;

namespace Murdoku.Characters
{
    public interface ICharacterPlacementCell
    {
        Vector2Int GridPosition { get; }
        bool IsPlaceable { get; }
        bool IsOccupied { get; }

        bool TryPlaceCharacter(CharacterData character);
        void RemoveCharacter();
    }
}
