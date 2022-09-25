﻿/*Copyright 2022 Christopher Beda

Licensed under the Apache License, Version 2.0 (the "License");
you may not use this file except in compliance with the License.
You may obtain a copy of the License at

   http://www.apache.org/licenses/LICENSE-2.0

Unless required by applicable law or agreed to in writing, software
distributed under the License is distributed on an "AS IS" BASIS,
WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
See the License for the specific language governing permissions and
limitations under the License.*/

using Audio;
using Platform.Local;
using Quartz.Inputs;
using Quartz.Inventory;
using System.Collections.Generic;
using System;
using System.Collections;

namespace Quartz
{
	public class LootContainer : global::XUiC_LootContainer, ILockableInventory
	{
		private const string TAG = "LootContainer";
        private const string lockedSlotsCvarName = "$varQuartzLootContainerLockedSlots";

        private XUiC_ComboBoxInt comboBox;
        private XUiC_ContainerStandardControls standardControls;

        private TileEntityLootContainer lootContainer;

        private int ignoredLockedSlots;

        private string searchResult;

		public override void Init()
		{
			base.Init();
			XUiController parent = GetParentByType<XUiC_LootWindow>();
			if (parent == null)
			{
				return;
			}

            standardControls = parent.GetChildByType<XUiC_ContainerStandardControls>();

            comboBox = standardControls.GetChildByType<XUiC_ComboBoxInt>();
            if (comboBox != null)
            {
                comboBox.OnValueChanged += OnLockedSlotsChange;
            }

            XUiC_TextInput searchInput = parent.GetChildByType<XUiC_TextInput>();
            if (searchInput != null)
            {
                searchInput.OnChangeHandler += OnSearchInputChange;
                if (searchInput.UIInput != null)
                {
                    searchInput.Text = "";
                }
            }

            if (standardControls != null && standardControls is ContainerStandardControls)
            {
                foreach (XUiController xUiController in GetItemStackControllers())
                {
                    xUiController.OnPress += OnItemStackPress;
                }
            }
        }

        public override void OnOpen()
        {
            base.OnOpen();
            if(standardControls is ContainerStandardControls)
            {
                standardControls.OnSortPressed = OnSortPressed;
            }
            QuartzInputManager.inventoryActions.Enabled = true;
        }

        public override void OnClose()
        {
            base.OnClose();
            QuartzInputManager.inventoryActions.Enabled = false;
            lootContainer = null;
        }

        public void SetCurrentTileEntity(TileEntityLootContainer container)
        {
            lootContainer = container;
            LoadLockedSlots();
        }

        protected void OnSearchInputChange(XUiController sender, string text, bool changeFromCode)
        {
            searchResult = text;
            FilterFromSearch(text);
        }

        protected void OnLockedSlotsChange(XUiController sender, long value, long newValue)
        {
            if (!(lootContainer is TileEntitySecureLootContainer) || !lootContainer.bPlayerStorage)
            {
                return;
            }

            for (int i = 0; i < itemControllers.Length; i++)
            {
                ItemStack itemStack = itemControllers[i] as ItemStack;
                if (itemStack != null)
                {
                    if (value > i && i >= newValue)
                    {
                        itemStack.IsALockedSlot = false;
                    }

                    if (newValue > i && i >= value)
                    {
                        itemStack.IsALockedSlot = true;
                    }
                }
            }

            ignoredLockedSlots = (int)newValue;

            SaveLockedSlots();
        }

        public override void HandleSlotChangedEvent(int slotNumber, global::ItemStack stack)
        {
            if (slotNumber < itemControllers.Length)
            {
                ItemStack itemStack = itemControllers[slotNumber] as ItemStack;
                FilterFromSearch(itemStack, !string.IsNullOrEmpty(searchResult), searchResult);
            }
        }

        public void UpdateFilterFromSearch()
        {
            FilterFromSearch(searchResult);
        }

        public int TotalLockedSlotsCount()
        {
            int count = 0;
            for (int i = 0; i < itemControllers.Length; i++)
            {
                if (itemControllers[i] is ItemStack itemStack && itemStack.IsALockedSlot)
                {
                    count++;
                }
            }

            return count;
        }

        public int IndividualLockedSlotsCount()
        {
            int count = 0;
            for (int i = 0; i < itemControllers.Length; i++)
            {
                if (i >= ignoredLockedSlots && itemControllers[i] is ItemStack itemStack && itemStack.IsALockedSlot)
                {
                    count++;
                }
            }

            return count;
        }

        public int UnlockedSlotCount()
        {
            int count = 0;
            for (int i = 0; i < itemControllers.Length; i++)
            {
                if (i >= ignoredLockedSlots && itemControllers[i] is ItemStack itemStack && !itemStack.IsALockedSlot)
                {
                    count++;
                }
            }

            return count;
        }

        protected void OnSortPressed(int ignoreSlots)
        {
            global::ItemStack[] array = SortUtil.CombineAndSortStacks(this, ignoreSlots);
            for (int i = 0; i < array.Length; i++)
            {
                lootContainer.UpdateSlot(i, array[i]);
            }
        }

        protected void OnItemStackPress(XUiController sender, int mouseButton)
        {
            if (lootContainer is TileEntitySecureLootContainer && sender is ItemStack itemStack && standardControls is ContainerStandardControls controls
                && (QuartzInputManager.inventoryActions.LockSlot.IsPressed || controls.IsIndividualSlotLockingAllowed()))
            {
                int index = Array.IndexOf(itemControllers, itemStack);
                if (index >= ignoredLockedSlots)
                {
                    itemStack.IsALockedSlot = !itemStack.IsALockedSlot;
                    Manager.PlayButtonClick();
                    SaveLockedSlots();
                }
            }
        }

        protected virtual void SaveLockedSlots()
        {
            if (lootContainer == null || !lootContainer.bPlayerStorage)
            {
                return;
            }

            BitArray bitArray = new BitArray(itemControllers.Length);
            for (int i = 0; i < bitArray.Length; i++)
            {
                ItemStack itemStack = itemControllers[i] as ItemStack;
                if (itemStack != null && itemStack.IsALockedSlot)
                {
                    bitArray.Set(i, true);
                }
            }

            SaveLockedSlotsData(bitArray);
        }

        protected virtual void LoadLockedSlots()
        {
            if (lootContainer == null)
            {
                return;
            }

            BitArray bitArray = LoadLockedSlotsData();

            for (int i = 0; i < itemControllers.Length; i++)
            {
                ItemStack itemStack = itemControllers[i] as ItemStack;
                if (itemStack != null)
                {
                    itemStack.IsALockedSlot = bitArray.Get(i);
                }
            }

            if (standardControls != null && comboBox != null)
            {
                standardControls.ChangeLockedSlots(ignoredLockedSlots);
                comboBox.Value = ignoredLockedSlots;
            }

            if (standardControls is ContainerStandardControls controls)
            {
                controls.ChangeLockedSlots(ignoredLockedSlots);
            }
        }

        private void SaveLockedSlotsData(BitArray bitArray)
        {
            TileEntitySecureLootContainer secureContainer = lootContainer as TileEntitySecureLootContainer;
            if (secureContainer == null || !lootContainer.bPlayerStorage)
            {
                return;
            }

            List<PlatformUserIdentifierAbs> userIds = secureContainer.GetUsers();
            byte[] bytes = new byte[(bitArray.Length - 1) / 8 + 1];
            bitArray.CopyTo(bytes, 0);

            string newUserId = lockedSlotsCvarName + "," + ignoredLockedSlots + "," + Convert.ToBase64String(bytes);

            for (int i = 0; i < userIds.Count; i++)
            {
                if (userIds[i] is UserIdentifierLocal userId)
                {
                    string[] idStrings = userId.PlayerName.Split(',');
                    if (idStrings[0] == lockedSlotsCvarName)
                    {
                        userIds[i] = new UserIdentifierLocal(newUserId);
                        return;
                    }
                }
            }

            userIds.Insert(0, new UserIdentifierLocal(newUserId));
        }

        private BitArray LoadLockedSlotsData()
        {
            TileEntitySecureLootContainer secureContainer = lootContainer as TileEntitySecureLootContainer;
            if(secureContainer == null)
            {
                return new BitArray(itemControllers.Length);
            }

            foreach (PlatformUserIdentifierAbs userId in secureContainer.GetUsers())
            {
                if (userId is UserIdentifierLocal user)
                {
                    string[] idStrings = user.PlayerName.Split(',');
                    if (idStrings[0] == lockedSlotsCvarName && idStrings.Length == 3)
                    {
                        ignoredLockedSlots = int.Parse(idStrings[1]);
                        byte[] bytes = Convert.FromBase64String(idStrings[2]);
                        return new BitArray(bytes);
                    }
                }
            }

            return new BitArray(itemControllers.Length);
        }

        private void FilterFromSearch(string search)
		{
			bool activeSearch = !string.IsNullOrEmpty(search);
			foreach (var itemController in itemControllers)
			{
				ItemStack itemStack = itemController as ItemStack;
				FilterFromSearch(itemStack, activeSearch, search);
			}
		}

		private void FilterFromSearch(ItemStack itemStack, bool activeSearch, string search)
		{
			if (itemStack == null)
			{
				return;
			}
			if (activeSearch)
			{
				itemStack.MatchesSearch = SearchUtil.MatchesSearch(itemStack.ItemStack, search);
			}
			else
			{
				itemStack.MatchesSearch = false;
			}
			itemStack.IsSearchActive = activeSearch;
		}
	}
}
