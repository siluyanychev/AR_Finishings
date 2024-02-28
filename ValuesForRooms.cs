﻿using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;
using System.Collections.Generic;
using System.Linq;

namespace AR_Finishings
{
    internal class ValuesForRooms
    {
        private Document _doc;

        private const string RoomFloorLayerName = "DPM_AR_Отделка.Полы";
        private const string RoomFloorLayerArea = "DPM_AR_Отделка.Полы.Площадь";
        private const string RoomCeilingLayerName = "DPM_AR_Отделка.Потолки";
        private const string RoomCeilingLayerArea = "DPM_AR_Отделка.Потолки.Площадь";
        private const string RoomCeilingLayerHeight = "DPM_AR_Отделка.Потолки.Высота";
        private const string RoomWallLayerName = "DPM_AR_Отделка.Стены";
        private const string RoomWallLayerArea = "DPM_AR_Отделка.Стены.Площадь";
        private const string RoomColumnLayerName = "DPM_AR_Отделка.Колонны";
        private const string RoomColumnLayerArea = "DPM_AR_Отделка.Колонны.Площадь";
        private const string RoomSkirtLayerName = "DPM_AR_Отделка.Плинтусы";
        private const string RoomSkirtaLayerLength = "DPM_AR_Отделка.Плинтусы.Длина";

        private const string RoomNumberParam = "DPM_AR_Отделка.Номер";
        private const string RoomNumbersParam = "DPM_AR_Отделка.Номера";
        private const string RoomNameParam = "DPM_AR_Отделка.Имя";
        private const string RoomNamesParam = "DPM_AR_Отделка.Имена";

        public ValuesForRooms(Document doc)
        {
            _doc = doc;
        }

        public List<Room> GetRooms()
        {
            // Получаем все помещения из документа
            FilteredElementCollector collector = new FilteredElementCollector(_doc);
            List<Room> allRooms = collector.OfClass(typeof(SpatialElement)).OfType<Room>().ToList();

            // Фильтруем помещения, оставляя только те, у которых площадь больше нуля
            List<Room> rooms = allRooms.Where(room => IsRoomAreaNonZero(room)).ToList();

            return rooms;
        }
        public void UpdateRoomParameters()
        {
            var rooms = GetRooms();
            string transactionName;

            if (rooms.Any())
            {
                // Получаем первый и последний номер помещения для именования транзакции
                string firstRoomNumber = rooms.First().Number;
                string lastRoomNumber = rooms.Last().Number;
                transactionName = $"Update floor types rooms from {firstRoomNumber} to {lastRoomNumber}";
            }
            else
            {
                transactionName = "Update floor types - No rooms found";
            }

            using (Transaction trans = new Transaction(_doc, transactionName))
            {
                trans.Start();

                foreach (var room in rooms)
                {
                    string roomNumber = room.Number; // Получаем номер комнаты
                    var roomFloors = GetFloors(roomNumber);
                    var roomCeilings = GetCeilings(roomNumber);
                    // Продолжаем с другими категориями, если необходимо

                    // Обновляем параметры слоев в помещении
                    UpdateRoomLayerName(room, roomFloors, RoomFloorLayerName);
                    // Продолжаем обновление для потолков и других элементов
                }

                trans.Commit();
            }
        }




        public List<Floor> GetFloors(string roomNumber)
        {
            FilteredElementCollector collector = new FilteredElementCollector(_doc);
            List<Floor> allFloors = collector.OfCategory(BuiltInCategory.OST_Floors).WhereElementIsNotElementType().OfType<Floor>().ToList();

            // Фильтруем полы, оставляя только те, у которых параметр "Номер" содержит заданное значение
            List<Floor> floors = allFloors.Where(floor => IsFloorWithRoomNumber(floor, roomNumber)).ToList();

            return floors;
        }

        // Метод для обновления имени слоя в помещении
        private void UpdateRoomLayerName(Room room, List<Floor> floors, string parameterName)
        {
            // Сортируем типы полов по имени и создаем строку для записи в параметр
            string typesName = string.Join("\n", floors.Select(f => f.FloorType.Name).Distinct().OrderBy(n => n));

            // Находим параметр помещения и обновляем его
            Parameter param = room.LookupParameter(RoomFloorLayerName);
            if (param != null && param.StorageType == StorageType.String)
            {
                param.Set(typesName);
            }
        }
        public List<Ceiling> GetCeilings(string roomNumber)
        {
            FilteredElementCollector collector = new FilteredElementCollector(_doc);
            List<Ceiling> allCeilings = collector.OfCategory(BuiltInCategory.OST_Ceilings).WhereElementIsNotElementType().OfType<Ceiling>().ToList();

            // Фильтруем полы, оставляя только те, у которых параметр "Номер" содержит заданное значение
            List<Ceiling> ceilings = allCeilings.Where(ceiling => IsCeilingWithRoomNumber(ceiling, roomNumber)).ToList();

            return ceilings;
        }




        // Rooms
        private bool IsRoomAreaNonZero(Room room)
        {
            // Получаем значение параметра "Площадь" помещения
            Parameter areaParameter = room.get_Parameter(BuiltInParameter.ROOM_AREA);

            // Проверяем, больше ли площадь нуля
            double area = areaParameter != null ? areaParameter.AsDouble() : 0;
            return area > 0;
        }
        // Floors
        private bool IsFloorWithRoomNumber(Floor floor, string roomNumber)
        {
            // Получаем значение параметра "Номер" для данного пола
            Parameter roomNumberParam = floor.LookupParameter(RoomNumberParam);

            // Проверяем, содержит ли параметр заданное значение
            string floorRoomNumber = roomNumberParam != null ? roomNumberParam.AsString() : string.Empty;
            return floorRoomNumber == roomNumber;
        }



        // Ceilings
        private bool IsCeilingWithRoomNumber(Ceiling ceiling, string roomNumber)
        {
            // Получаем значение параметра "Номер" для данного пола
            Parameter roomNumberParam = ceiling.LookupParameter(RoomNumberParam);

            // Проверяем, содержит ли параметр заданное значение
            string ceilingRoomNumber = roomNumberParam != null ? roomNumberParam.AsString() : string.Empty;
            return ceilingRoomNumber == roomNumber;
        }


    }
}
