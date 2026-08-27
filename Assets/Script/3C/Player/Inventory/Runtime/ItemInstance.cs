using System;
using System.Collections.Generic;

namespace CGame
{
    public class ItemInstance
    {
        private readonly List<string> attachmentIds = new List<string>();

        protected internal ItemInstance(ItemInstanceHandle handle, ItemDefinition definition)
        {
            Handle = handle;
            Definition = definition ?? throw new ArgumentNullException(nameof(definition));
        }

        public ItemInstanceHandle Handle { get; }

        public ItemDefinition Definition { get; }

        public int MagazineAmmo { get; private set; }

        public int ReserveAmmo { get; private set; }

        public IReadOnlyList<string> AttachmentIds => attachmentIds;

        public void SetAmmo(int magazineAmmo, int reserveAmmo)
        {
            if (magazineAmmo < 0 || reserveAmmo < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(magazineAmmo));
            }

            MagazineAmmo = magazineAmmo;
            ReserveAmmo = reserveAmmo;
        }

        public bool TryConsumeMagazineAmmo(int amount = 1)
        {
            if (amount <= 0 || MagazineAmmo < amount)
            {
                return false;
            }

            MagazineAmmo -= amount;
            return true;
        }

        public int ReloadMagazine(int capacity)
        {
            int needed = Math.Max(0, capacity - MagazineAmmo);
            int loaded = Math.Min(needed, ReserveAmmo);
            MagazineAmmo += loaded;
            ReserveAmmo -= loaded;
            return loaded;
        }

        public void SetAttachments(IEnumerable<string> attachments)
        {
            attachmentIds.Clear();
            if (attachments == null)
            {
                return;
            }

            foreach (string attachment in attachments)
            {
                if (!string.IsNullOrWhiteSpace(attachment) && !attachmentIds.Contains(attachment))
                {
                    attachmentIds.Add(attachment);
                }
            }
        }
    }
}
