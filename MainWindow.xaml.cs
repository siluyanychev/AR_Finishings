﻿using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.DB.Architecture;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Resources;
using System.Text;
using System.Windows;

namespace AR_Finishings
{

    public partial class MainWindow
    {
        // Классы
        public ElementTypeSelector ets { get; private set; }
        // Сокращения
        private ResourceManager rm = new ResourceManager("AR_Finishings.Strings", Assembly.GetExecutingAssembly());
        private CultureInfo ci = CultureInfo.CurrentCulture;
        private StringBuilder resultMessage = new StringBuilder();
        public ExternalCommandData commandData;
        private Document mainDocument;
        private IList<ElementId> _selectedRoomIds;
        // связка из разметки
        public string FloorsOffset { get; set; } = "0"; // Значение по умолчанию для смещения пола
        public string CeilingsHeight { get; set; } = "3200"; // Значение по умолчанию для высоты потолков
        public string WallsOffset { get; set; } = "110"; // Значение по умолчанию для отступа по высоте стены за потолком 
        public string SkirtsHeight { get; set; } = "80"; // Значение по умолчанию для высоты плинтусов
        private void CheckBox_GetParameters(object sender, RoutedEventArgs e)
        {
            // Логика обработки события для CheckBox_ValsForElements
        }
        private void CheckBox_ValsForElements(object sender, RoutedEventArgs e)
        {
            // Логика обработки события для CheckBox_ValsForElements
        }

        private void CheckBox_ValsForRooms(object sender, RoutedEventArgs e)
        {
            // Логика обработки события для CheckBox_ValsForRooms
        }

        private void UpdateFloorTypes()
        {
            var floorTypes = ets.GetFloorTypes(mainDocument);
            selectFloorsComboBox.ItemsSource = floorTypes;
            selectFloorsComboBox.DisplayMemberPath = "Name"; // Установить отображаемое имя
        }
        private void UpdateCeilingTypes()
        {
            var ceilingTypes = ets.GetCeilingTypes(mainDocument);
            selectCeilingsComboBox.ItemsSource = ceilingTypes;
            selectCeilingsComboBox.DisplayMemberPath = "Name";
        }
        private void UpdateWallTypes()
        {
            var wallTypes = ets.GetWallTypes(mainDocument);
            selectWallsComboBox.ItemsSource = wallTypes;
            selectWallsComboBox.DisplayMemberPath = "Name";
        }
        private void UpdateSkirtTypes()
        {
            var wallTypes = ets.GetWallTypes(mainDocument);
            selectSkirtsComboBox.ItemsSource = wallTypes;
            selectSkirtsComboBox.DisplayMemberPath = "Name";
        }
        public MainWindow(ExternalCommandData commandData)
        {
            InitializeComponent();
            DataContext = this;

            // Получаем выбранные помещения из Revit перед запуском WPF окна
            UIDocument uidoc = commandData.Application.ActiveUIDocument;
            _selectedRoomIds = uidoc.Selection.GetElementIds().Where(id =>
                uidoc.Document.GetElement(id) is Room).ToList();

            if (!_selectedRoomIds.Any())
            {
                // Если не были выбраны помещения, показать сообщение и закрыть плагин
                MessageBox.Show("Please select rooms before running this plugin.");
                this.Close();
                return;
            }

            // Инициализация классов для работы с элементами
            ets = new ElementTypeSelector();
            this.commandData = commandData;
            this.mainDocument = uidoc.Document;

            // Подгрузка типов элементов для ComboBox
            UpdateFloorTypes();
            UpdateCeilingTypes();
            UpdateWallTypes();
            UpdateSkirtTypes();
        }

        private void RunButton_Click(object sender, RoutedEventArgs e)
        {
            // Получаем выбранный тип пола из ComboBox
            FloorType selectedFloorType = selectFloorsComboBox.SelectedItem as FloorType;

            // Получаем значения смещения из TextBox, предварительно проверив их на корректность
            if (!double.TryParse(floorOffsetTextBox.Text, out double floorOffset))
            {
                MessageBox.Show("Please enter a valid number for floor offset.");
                return;
            }

            // Используем метод для генерации полов с использованием выбранных параметров
            RoomBoundaryFloorGenerator generator = new RoomBoundaryFloorGenerator(mainDocument);
            generator.CreateFloors(_selectedRoomIds, selectedFloorType);
        }

    }
}
