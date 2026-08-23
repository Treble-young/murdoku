using System;
using System.Collections.Generic;
using UnityEngine;

namespace Murdoku.Characters
{
    [CreateAssetMenu(
        fileName = "CharacterPortraitCatalog",
        menuName = "Murdoku/Characters/Portrait Catalog")]
    public sealed class CharacterPortraitCatalog : ScriptableObject
    {
        [Serializable]
        public sealed class Entry
        {
            [SerializeField] private string portraitId;
            [SerializeField] private CharacterGender gender;
            [SerializeField] private Sprite portrait;

            public string PortraitId => portraitId;
            public CharacterGender Gender => gender;
            public Sprite Portrait => portrait;
            public bool IsUsable => portrait != null &&
                                    (gender == CharacterGender.Male || gender == CharacterGender.Female);
        }

        [SerializeField] private List<Entry> entries = new List<Entry>();

        public IReadOnlyList<Entry> Entries => entries;

        public void CopyUsableEntriesTo(List<Entry> destination)
        {
            if (destination == null)
            {
                throw new ArgumentNullException(nameof(destination));
            }

            destination.Clear();
            foreach (Entry entry in entries)
            {
                if (entry != null && entry.IsUsable)
                {
                    destination.Add(entry);
                }
            }
        }

        public bool TryGetEntry(Sprite portrait, out Entry match)
        {
            foreach (Entry entry in entries)
            {
                if (entry != null && entry.IsUsable && entry.Portrait == portrait)
                {
                    match = entry;
                    return true;
                }
            }

            match = null;
            return false;
        }
    }
}
