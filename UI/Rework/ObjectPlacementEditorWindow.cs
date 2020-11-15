﻿using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Windows.Forms;
using MinishMaker.Core;
using MinishMaker.Core.ChangeTypes;
using MinishMaker.Utilities;
using MinishMaker.Utilities.Rework;
using static MinishMaker.Utilities.Rework.ListFileParser;

namespace MinishMaker.UI.Rework
{
    public partial class ObjectPlacementEditorWindow : SubWindow
    {
        private int objectIndex = 0;
        private int listIndex = 0;
        private int[] listKeys;
        private int currentListEntryAmount = 0;
        private Core.Rework.Room currentRoom = null;
        private List<byte> currentListEntry = null;
        private bool shouldTrigger = false;
        private List<byte> copy = null;
        public ObjectPlacementEditorWindow()
        {
            InitializeComponent();
        }

        public override void Setup()
        {
            currentRoom = MainWindow.instance.currentRoom;
            if (currentRoom == null)
            {
                return;
            }
            objectIndex = 0;
            listIndex = 0;
            listKeys = currentRoom.MetaData.GetListInformationKeys();

            SetData();
        }

        public override void Cleanup()
        {
            
        }

        public void SetData()
        {
            shouldTrigger = false;
            newButton.Enabled = true;

            nextListButton.Enabled = listIndex < listKeys.Length - 1;
            prevListButton.Enabled = listIndex != 0;

            if (listKeys.Length == 0)
            {
                indexLabel.Text = "0";
                copyButton.Enabled = false;
                pasteButton.Enabled = false;

                removeButton.Enabled = false;
                prevButton.Enabled = false;
                nextButton.Enabled = false;

                ((MainWindow)Application.OpenForms[0]).HighlightListObject(-1, -1);
                return;
            }
            var currentListNumber = listKeys[listIndex];
            currentListEntryAmount = currentRoom.MetaData.GetListEntryAmount(currentListNumber);

            listIndexLabel.Text = (currentListNumber + " ");

            indexLabel.Text = objectIndex + "";

            nextButton.Enabled = objectIndex != currentListEntryAmount - 1;
            prevButton.Enabled = objectIndex != 0;

            removeButton.Enabled = currentListEntryAmount != 0;

            copyButton.Enabled = true;
            pasteButton.Enabled = copy.Count != 0;

            currentListEntry = currentRoom.MetaData.GetListInformationEntry(currentListNumber, objectIndex);
            GetRepresentation();
            shouldTrigger = true;
        }

        private void newButton_Click(object sender, EventArgs e)
        {
            if (listKeys.Length == 0)
            {
                return;
            }

            AddChange();
            currentRoom.MetaData.AddNewListInformation(listKeys[listIndex], objectIndex);

            objectIndex += 1;
            SetData();
        }

        private void removeButton_Click(object sender, EventArgs e)
        {
            if (currentListEntryAmount == 0)
            {
                return;
            }

            AddChange();
            currentRoom.MetaData.RemoveListInformation(listKeys[listIndex], objectIndex);

            if (objectIndex != 0)
                objectIndex -= 1;

            SetData();
        }

        private void nextButton_Click(object sender, EventArgs e)
        {
            objectIndex += 1;
            SetData();
        }

        private void prevButton_Click(object sender, EventArgs e)
        {
            objectIndex -= 1;
            SetData();
        }

        private void prevListButton_Click(object sender, EventArgs e)
        {
            listIndex -= 1;
            objectIndex = 0;
            SetData();
        }

        private void nextListButton_Click(object sender, EventArgs e)
        {
            listIndex += 1;
            objectIndex = 0;
            SetData();
        }

        private void AddChange()
        {

        }

        private void copyButton_Click(object sender, EventArgs e)
        {
            copy = new List<byte>(currentListEntry);
            pasteButton.Enabled = true;
        }

        private void pasteButton_Click(object sender, EventArgs e)
        {
            currentListEntry.Clear();
            currentListEntry.AddRange(copy);
            AddChange();
            SetData();
        }

        private void ChangedHandler(FormElement element, int newValue)
        {
            if (!shouldTrigger)
                return;

            //changeAction.Invoke();

            AddChange();
        }

        //because why type the same 4 times
        private void HandleByteString(ref TextBox textBox, ref byte property)
        {
            try
            {
                var newVal = Convert.ToByte(textBox.Text, 16);

                property = newVal;
            }
            catch
            {
                textBox.Text = property.Hex();
            }
        }

        //or 5 times
        private void HandleUInt16String(ref TextBox textBox, ref ushort property)
        {
            try
            {
                var newVal = Convert.ToUInt16(textBox.Text, 16);

                property = newVal;
            }
            catch
            {
                textBox.Text = property.Hex();
            }
        }

        private static bool ParseInt(string numberString, ref int value)
        {
            if (numberString.StartsWith("0x", StringComparison.InvariantCultureIgnoreCase))
            {
                return int.TryParse(numberString.Substring(2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out value);
            }
            else
            {
                return int.TryParse(numberString, out value);
            }
        }

        private void GetRepresentation()
        {
            List<Filter> filters = new List<Filter>();
            var currentFilter = ListFileParser.topFilter;
            filters.Add(currentFilter);
            
            while (currentFilter.children.Length != 0)
            {
                var dtt = currentFilter.defaultTargetType;
                var dtp = currentFilter.defaultTargetPos;
                Filter nextFilter = null;

                int defaultDataValue = -1;
                if (dtt.ToLower() == "list" || dtp != 0)
                {
                    defaultDataValue = GetTargetValue(dtt, dtp);
                }

                foreach (var filter in currentFilter.children)
                {
                    var tt = filter.targetType;
                    var tp = filter.targetPos;

                    if (filter.targetValues.Length == 0)//default if nothing else is found
                    {
                        nextFilter = filter;
                        continue;
                    }
                    int dataValue = 0;
                    if (defaultDataValue != -1 && tt.ToLower() != "list" && tp == 0) //no type and pos set so use default target type and pos
                    {
                        dataValue = defaultDataValue;
                    }
                    else
                    {
                        dataValue = GetTargetValue(tt, tp);
                    }

                    bool found = false;
                    foreach (var targetVal in filter.targetValues)
                    {
                        var outValue = 0;
                        var success = ParseInt(targetVal, ref outValue);
                        if(!success)
                        {
                            throw new FormatException("This should never happen as it is pre-validated.");
                        }

                        if (outValue == dataValue)
                        {
                            found = true;
                            break;
                        }
                    }

                    if(found)
                    {
                        nextFilter = filter;
                        break;
                    }
                }

                if(nextFilter == null)
                {
                    throw new MissingMemberException($"No matches were made and no default filter was found in the children of a filter with defaultTarget:{currentFilter.targetType} and defaultPos:{currentFilter.targetPos}");
                }

                currentFilter = nextFilter;
                filters.Add(nextFilter);
            }
            //done filtering, get all elements
            var tabIndex = 30;
            foreach(var filter in filters)
            {
                foreach (var element in filter.elements)
                {
                    CreateElement(element, ref tabIndex);
                }
            }
        }

        private void CreateElement(FormElement element, ref int tabIndex)
        {
            switch(element.type.ToLower())
            {
                case "enum":
                    CreateEnumElement(element, tabIndex);
                    tabIndex += 1;
                    break;
                case "number":
                    CreateNumberElement(element, tabIndex);
                    tabIndex += 1;
                    break;
                case "hexnumber":
                    CreateHexNumberElement(element, tabIndex);
                    tabIndex += 1;
                    break;
                case "label":
                    CreateLabelElement(element);
                    break;
                case "xmarker":
                    break;
                case "ymarker":
                    break;
                default:
                    throw new ArgumentException($"This should not be possible as validation already happened. {element.type}");
            }
        }

        private void CreateLabelElement(FormElement element)
        {
            Label labelElement = new Label();
            labelElement.AutoSize = true;
            labelElement.Margin = new Padding(4, 0, 4, 0);
            labelElement.Text = element.label;
            labelElement.Anchor = AnchorStyles.Right;
            elementTable.Controls.Add(labelElement, element.collumn, element.row);
        }

        private void CreateEnumElement(FormElement element, int tabIndex)
        {
            var collumn = element.collumn;
            if(element.label.Length != 0)
            {
                CreateLabelElement(element);
                collumn += 1;
            }
            int value = GetTargetValue(element.valueType, element.valuePos);
            var enumSource = ListFileParser.GetEnum(element.enumType);
            var enumObject = enumSource[value];
            var enumElement = new ComboBox();
            enumElement.FormattingEnabled = true;
            enumElement.DisplayMember = "Value";
            enumElement.ValueMember = "Key";
            enumElement.DataSource = new BindingSource(enumSource, null);
            
            enumElement.SelectedIndex = enumElement.Items.IndexOf(enumObject);
            enumElement.TabIndex = tabIndex;

            //TODO: add change logic
            enumElement.SelectedIndexChanged += new EventHandler((object o, EventArgs e) => { ChangedHandler(element, (int)enumElement.SelectedValue); });

            elementTable.Controls.Add(enumElement, collumn, element.row);
        }

        private void CreateHexNumberElement(FormElement element, int tabIndex)
        {
            int value = GetTargetValue(element.valueType, element.valuePos);
            var collumn = element.collumn;
            if (element.label.Length != 0)
            {
                CreateLabelElement(element);
                collumn += 1;
            }

            var hexNumberElement = new TextBox();
            hexNumberElement.TabIndex = tabIndex;
            hexNumberElement.Text = "0x" + value.Hex();
            hexNumberElement.LostFocus += new EventHandler((object o, EventArgs e) => 
                {
                    var value = 0;
                    var success = ParseInt(hexNumberElement.Text, ref value);
                    if (!success)
                    {
                        hexNumberElement.Text = "" + GetTargetValue(element.valueType, element.valuePos);
                    }
                    ChangedHandler(element, value);
                }
            );

            elementTable.Controls.Add(hexNumberElement, collumn, element.row);
        }

        private void CreateNumberElement(FormElement element, int tabIndex)
        {
            var value = GetTargetValue(element.valueType, element.valuePos);
            var collumn = element.collumn;
            if (element.label.Length != 0)
            {
                CreateLabelElement(element);
                collumn += 1;
            }

            var numberElement = new TextBox();
            numberElement.TabIndex = tabIndex;
            numberElement.Text = "" + value;
            numberElement.LostFocus += new EventHandler((object o, EventArgs e) =>
                {
                    var success = int.TryParse(numberElement.Text,out var value);
                    if (!success)
                    {
                        numberElement.Text = ""+GetTargetValue(element.valueType, element.valuePos);
                    }
                    ChangedHandler(element, value);
                }
            );

            elementTable.Controls.Add(numberElement, collumn, element.row);
        }

        private int GetTargetValue(string targetType, int targetPos)
        {
            var dataValue = 0;
            switch (targetType.ToLower())
            {
                case "bit":
                    var bytePos = (targetPos - 1) / 8;
                    var bitPos = targetPos - (bytePos * 8);
                    dataValue = (currentListEntry[bytePos] >> (bitPos - 1)) & 1;
                    break;
                case "list":
                    dataValue = listIndex;
                    break;
                case "int":
                    dataValue += currentListEntry[targetPos] + currentListEntry[targetPos + 1] << 8 + currentListEntry[targetPos + 2] << 16 + currentListEntry[targetPos + 3] << 24;
                    break;
                case "tri":
                    dataValue += currentListEntry[targetPos] + currentListEntry[targetPos + 1] << 8 + currentListEntry[targetPos + 2] << 16;
                    break;
                case "short":
                    dataValue += currentListEntry[targetPos] + currentListEntry[targetPos + 1] << 8;
                    break;
                case "byte":
                    dataValue += currentListEntry[targetPos];
                    break;
                default:
                    throw new ArgumentException($"Unknown targetType ({targetType}). How did you get here? this should have been checked already.");
            }
            return dataValue;
        }
    }
}
