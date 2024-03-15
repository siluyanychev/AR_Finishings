using Autodesk.Revit.DB;
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
using System.Windows.Controls;
using System.Runtime.CompilerServices;

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
        public double CeilingsHeight { get; set; } = 3100; // Значение по умолчанию для высоты потолков
        public double WallsOffset { get; set; } = 100; // Значение по умолчанию для отступа по высоте стены за потолком 
        public double SkirtsHeight { get; set; } = 100; // Значение по умолчанию для высоты плинтусов

        private bool _isParametersCheckboxChecked;
        private bool _isValsForElementsChecked = false;
        private bool _isValsForRoomsChecked = false;




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
            var skirtTypes = ets.GetWallTypes(mainDocument);
            selectSkirtsComboBox.ItemsSource = skirtTypes;
            selectSkirtsComboBox.DisplayMemberPath = "Name";
        }
        private void CheckBox_ValsForElements_Checked(object sender, RoutedEventArgs e)
        {
            _isValsForElementsChecked = true;
        }
        private void CheckBox_ValsForRooms_Checked(object sender, RoutedEventArgs e)
        {
            _isValsForRoomsChecked = true;
        }

        public MainWindow(ExternalCommandData commandData)
        {
            InitializeComponent();
            DataContext = this;

            // Получаем выбранные помещения из Revit перед запуском WPF окна
            UIDocument uidoc = commandData.Application.ActiveUIDocument;
            _selectedRoomIds = uidoc.Selection.GetElementIds().Where(id =>
                uidoc.Document.GetElement(id) is Room).ToList();

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
        private void CheckBox_GetParameters(object sender, RoutedEventArgs e)
        {
            var checkBox = sender as CheckBox;
            _isParametersCheckboxChecked = checkBox.IsChecked == true;
        }

        private void RunButton_Click(object sender, RoutedEventArgs e)
        {
            // Получаем выбранный тип пола из ComboBox

            FloorType selectedFloorType = selectFloorsComboBox.SelectedItem as FloorType;
            CeilingType selectedCeilingType = selectCeilingsComboBox.SelectedItem as CeilingType;
            WallType selectedWallType = selectWallsComboBox.SelectedItem as WallType;
            WallType selectedSkirtType = selectSkirtsComboBox.SelectedItem as WallType;



            if (_isParametersCheckboxChecked)
            {
                // Если чекбокс отмечен, вызываем методы из класса Preparations
                Preparations preparation = new Preparations();
                preparation.GetParameters(mainDocument);
            }

            if (selectedCeilingType != null)
            {
                RoomBoundaryCeilingGenerator ceilingGenerator = new RoomBoundaryCeilingGenerator(mainDocument, CeilingsHeight);
                ceilingGenerator.CreateCeilings(_selectedRoomIds, selectedCeilingType);
            }

            // Используем метод для генерации полов с использованием выбранных параметров
            if (selectedFloorType != null)
            {
                // Вызываем метод для генерации полов с использованием выбранных параметров
                RoomBoundaryFloorGenerator floorGenerator = new RoomBoundaryFloorGenerator(mainDocument);
                floorGenerator.CreateFloors(_selectedRoomIds, selectedFloorType);
                floorGenerator.CheckFloorsAndDoorsIntersection();
                floorGenerator.FloorCutDoor();
            }
            // Используем метод для генерации полов с использованием выбранных параметров
            if (selectedWallType != null)
            {
                // The wall generator needs to accept the ceiling height parameter.
                RoomBoundaryWallGenerator wallGenerator = new RoomBoundaryWallGenerator(mainDocument, (CeilingsHeight + WallsOffset));
                wallGenerator.CreateWalls(_selectedRoomIds, selectedWallType);
            }
            // Внутри метода RunButton_Click перед созданием skirtGenerator
            Room firstSelectedRoom = mainDocument.GetElement(_selectedRoomIds.First()) as Room;
            ElementId levelId = firstSelectedRoom.LevelId;

            if (selectedSkirtType != null)
            {
                // The wall generator needs to accept the ceiling height parameter.
                RoomBoundarySkirtGenerator skirtGenerator = new RoomBoundarySkirtGenerator(mainDocument, SkirtsHeight);
                skirtGenerator.CreateWalls(_selectedRoomIds, selectedSkirtType);
                skirtGenerator.CheckWallsAndDoorsIntersection(); // Сначала проверяем пересечения
                skirtGenerator.DivideWallsAtDoors(); // Затем разделяем стены;
            }
        }
        // Update
        private void CheckBox_ValsForElements(object sender, RoutedEventArgs e)
        {
            _isValsForElementsChecked = true;

        }
        private void CheckBox_ValsForRooms(object sender, RoutedEventArgs e)
        {
            _isValsForRoomsChecked = true;
        }
        private void UpdateButton_Click(object sender, RoutedEventArgs e)
        {
            if (_isValsForElementsChecked)
            {
                var valuesForFloors = new ValuesForFloors(mainDocument);
                valuesForFloors.SetToGeom();
                var valuesForCeilings = new ValuesForCeilings(mainDocument);
                valuesForCeilings.SetToGeom();
                var valuesForWalls = new ValuesForWalls(mainDocument);
                valuesForWalls.SetToGeom();



                // Сброс флага, если нужно выполнить действие только один раз
                _isValsForElementsChecked = false;
            }
            if (_isValsForRoomsChecked)
            {
                var valuesForRooms = new ValuesForRooms(mainDocument);
                valuesForRooms.UpdateRoomParameters();

                // Сброс флага, если нужно выполнить действие только один раз
                _isValsForRoomsChecked = false;
            }
            this.Close();
        }

    }
}
