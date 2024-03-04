using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;
using Autodesk.Revit.UI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

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
        private const string RoomSkirtLayerLength = "DPM_AR_Отделка.Плинтусы.Длина";

        private const string RoomNumberParam = "DPM_AR_Отделка.Номер";


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
                transactionName = $"Update bounding types rooms from {firstRoomNumber} to {lastRoomNumber}";
            }
            else
            {
                transactionName = "Update bounding types - No rooms found";
            }

            using (Transaction trans = new Transaction(_doc, transactionName))
            {
                trans.Start();

                foreach (var room in rooms)
                {
                    string roomNumber = room.Number; // Получаем номер комнаты
                    var roomFloors = GetFloors(roomNumber);
                    var roomCeilings = GetCeilings(roomNumber);

                    List<Wall> allWalls = GetWalls(roomNumber);
                    // Генерируем отфильтрованные списки
                    List<Wall> finishingMainWalls = GetFinishingMainWalls(allWalls);
                    List<Wall> finishingColumnWalls = GetFinishingColumnWalls(allWalls);
                    List<Wall> finishingSkirtWalls = GetFinishingSkirtWalls(allWalls);
                    // Продолжаем с другими категориями, если необходимо

                    // Обновляем параметры слоев в помещении
                    UpdateRoomFloorLayerName(room, roomFloors, RoomFloorLayerName);
                    UpdateRoomFloorLayerArea(room, roomFloors, RoomFloorLayerArea);
                    UpdateRoomCeilingLayerName(room, roomCeilings, RoomCeilingLayerName);
                    UpdateRoomCeilingLayerArea(room, roomCeilings, RoomCeilingLayerArea);
                    UpdateRoomCeilingHeight(room, roomCeilings, RoomCeilingLayerHeight);
                    UpdateRoomWallLayerName(room, finishingMainWalls, RoomWallLayerName);
                    UpdateRoomWallLayerArea(room, finishingMainWalls, RoomWallLayerArea);
                    UpdateRoomColumnLayerName(room, finishingColumnWalls, RoomColumnLayerName);
                    UpdateRoomColumnLayerArea(room, finishingColumnWalls, RoomColumnLayerArea);
                    UpdateRoomSkirtLayerName(room, finishingSkirtWalls, RoomSkirtLayerName);
                    UpdateRoomSkirtLayerLength(room, finishingSkirtWalls, RoomSkirtLayerLength);
                }

                trans.Commit();
            }
        }



        // Getters
        public List<Floor> GetFloors(string roomNumber)
        {
            FilteredElementCollector collector = new FilteredElementCollector(_doc);
            List<Floor> allFloors = collector.OfCategory(BuiltInCategory.OST_Floors).WhereElementIsNotElementType().OfType<Floor>().ToList();

            // Фильтруем полы, оставляя только те, у которых параметр "Номер" содержит заданное значение
            List<Floor> floors = allFloors.Where(floor => IsFloorWithRoomNumber(floor, roomNumber)).ToList();

            return floors;
        }
        public List<Ceiling> GetCeilings(string roomNumber)
        {
            FilteredElementCollector collector = new FilteredElementCollector(_doc);
            List<Ceiling> allCeilings = collector.OfCategory(BuiltInCategory.OST_Ceilings).WhereElementIsNotElementType().OfType<Ceiling>().ToList();

            // Фильтруем полы, оставляя только те, у которых параметр "Номер" содержит заданное значение
            List<Ceiling> ceilings = allCeilings.Where(ceiling => IsCeilingWithRoomNumber(ceiling, roomNumber)).ToList();

            return ceilings;
        }
        public List<Wall> GetWalls(string roomNumber)
        {
            FilteredElementCollector collector = new FilteredElementCollector(_doc);
            List<Wall> allWalls = collector.OfCategory(BuiltInCategory.OST_Walls)
                                            .WhereElementIsNotElementType()
                                            .OfType<Wall>()
                                            .ToList();

            // Фильтруем стены, оставляя только те, у которых параметр "Номер" содержит заданное значение
            // и имя типа начинается с "АР_О"
            List<Wall> walls = allWalls.Where(wall =>
                IsWallWithRoomNumber(wall, roomNumber) &&
                wall.WallType != null &&
                wall.WallType.Name.StartsWith("АР_О"))
                .ToList();

            return walls;



        }
        // Метод для получения основных отделочных стен
        public List<Wall> GetFinishingMainWalls(List<Wall> walls)
        {
            return walls.Where(wall =>
                wall.get_Parameter(BuiltInParameter.WALL_ATTR_ROOM_BOUNDING).AsInteger() == 1 &&
                wall.get_Parameter(BuiltInParameter.WALL_USER_HEIGHT_PARAM).AsDouble() > 150 / 304.8 && // Переводим 150мм в футы
                wall.get_Parameter(BuiltInParameter.ALL_MODEL_INSTANCE_COMMENTS).AsString() != "Колонна")
                .ToList();
        }
        // Метод для получения стен-колонн
        public List<Wall> GetFinishingColumnWalls(List<Wall> walls)
        {
            return walls.Where(wall =>
                wall.get_Parameter(BuiltInParameter.WALL_ATTR_ROOM_BOUNDING).AsInteger() == 1 &&
                wall.get_Parameter(BuiltInParameter.WALL_USER_HEIGHT_PARAM).AsDouble() > 150 / 304.8 && // Переводим 150мм в футы
                wall.get_Parameter(BuiltInParameter.ALL_MODEL_INSTANCE_COMMENTS).AsString() == "Колонна") 
                .ToList();
        }

        // Метод для получения плинтусов
        public List<Wall> GetFinishingSkirtWalls(List<Wall> walls)
        {
            return walls.Where(wall =>
                wall.get_Parameter(BuiltInParameter.WALL_ATTR_ROOM_BOUNDING).AsInteger() == 0 &&
                wall.get_Parameter(BuiltInParameter.WALL_USER_HEIGHT_PARAM).AsDouble() <= 150 / 304.8) // Переводим 150мм в футы
                .ToList();
        }


        // Floors
        private void UpdateRoomFloorLayerName(Room room, List<Floor> floors, string parameterName)
        {
            // Инициализируем словарь для сопоставления марок с их составом
            var typesDescriptions = new Dictionary<string, string>();

            foreach (Floor floor in floors)
            {
                Parameter markParam = floor.FloorType.get_Parameter(new Guid("2204049c-d557-4dfc-8d70-13f19715e46d")); // ADSK_Этаж
                Parameter compositionParam = floor.FloorType.get_Parameter(new Guid("92f979b3-1252-42e1-92aa-b4a9337e285f")); // Слои.Состав

                if (markParam != null && compositionParam != null)
                {
                    string mark = markParam.AsString();
                    string composition = compositionParam.AsString();

                    if (mark != null) // Проверка на null
                    {
                        typesDescriptions[mark] = composition;
                    }
                    else
                    {
                        // Отображаем TaskDialog, предупреждающий пользователя
                        TaskDialog dialog = new TaskDialog("Неопределенная марка")
                        {
                            MainInstruction = "Заполните 'ADSK_марка' в полах",
                            MainContent = "Один из полов не имеет определенной марки, что может вызвать ошибки.",
                            CommonButtons = TaskDialogCommonButtons.Close,
                            DefaultButton = TaskDialogResult.Close
                        };
                        dialog.Show();
                    }
                }
            }

            // Сортируем словарь по ключам (маркам) и формируем итоговую строку
            var sortedDescriptions = typesDescriptions.OrderBy(kvp => kvp.Key);
            StringBuilder typesDescription = new StringBuilder();
            foreach (var kvp in sortedDescriptions)
            {
                typesDescription.AppendLine($"{kvp.Key}:\n{kvp.Value}");
            }

            // Находим параметр помещения и обновляем его многострочным текстом
            Parameter param = room.LookupParameter(RoomFloorLayerName);
            if (param != null && param.StorageType == StorageType.String)
            {
                param.Set(typesDescription.ToString());
            }
        }
        private void UpdateRoomFloorLayerArea(Room room, List<Floor> floors, string parameterName)
        {
            var typesDescriptions = new Dictionary<string, double>();

            foreach (Floor floor in floors)
            {
                Parameter markParam = floor.FloorType.get_Parameter(new Guid("2204049c-d557-4dfc-8d70-13f19715e46d")); // ADSK_Этаж
                if (markParam == null || string.IsNullOrEmpty(markParam.AsString()))
                {
                    // Отображаем TaskDialog, предупреждающий пользователя
                    TaskDialog dialog = new TaskDialog("Неопределенная марка")
                    {
                        MainInstruction = "Заполните 'ADSK_марка' в полах",
                        MainContent = "Один из полов не имеет определенной марки, что может вызвать ошибки.",
                        CommonButtons = TaskDialogCommonButtons.Close,
                        DefaultButton = TaskDialogResult.Close
                    };
                    dialog.Show();
                    continue; // Пропускаем этот пол и продолжаем с остальными
                }

                string mark = markParam.AsString();
                double area = floor.get_Parameter(BuiltInParameter.HOST_AREA_COMPUTED).AsDouble() * 0.092903; // Преобразуем площадь в метры квадратные

                if (!typesDescriptions.ContainsKey(mark))
                {
                    typesDescriptions[mark] = area;
                }
                else
                {
                    typesDescriptions[mark] += area; // Суммируем площади одинаковых типов полов
                }
            }

            var sortedDescriptions = typesDescriptions.OrderBy(kvp => kvp.Key);
            StringBuilder typesDescription = new StringBuilder();
            foreach (var kvp in sortedDescriptions)
            {
                // Форматируем строку с добавлением "м²" к значению площади
                typesDescription.AppendLine($"{kvp.Key}: {kvp.Value:F2} м²");
            }

            Parameter param = room.LookupParameter(parameterName);
            if (param != null && param.StorageType == StorageType.String)
            {
                param.Set(typesDescription.ToString());
            }
        }
        // Ceilings
        private void UpdateRoomCeilingLayerName(Room room, List<Ceiling> ceilings, string parameterName)
        {
            // Инициализируем словарь для сопоставления марок с их составом
            var typesDescriptions = new Dictionary<string, string>();

            foreach (Ceiling ceiling in ceilings)
            {
                // Получаем тип потолка
                ElementType ceilingType = _doc.GetElement(ceiling.GetTypeId()) as ElementType;
                if (ceilingType != null)
                {
                    Parameter markParam = ceilingType.get_Parameter(new Guid("2204049c-d557-4dfc-8d70-13f19715e46d")); // ADSK_Этаж
                    Parameter compositionParam = ceilingType.get_Parameter(new Guid("92f979b3-1252-42e1-92aa-b4a9337e285f")); // Слои.Состав

                    if (markParam != null && compositionParam != null)
                    {
                        string mark = markParam.AsString();
                        string composition = compositionParam.AsString();

                        if (!string.IsNullOrEmpty(mark))
                        {
                            typesDescriptions[mark] = composition;
                        }
                        else
                        {
                            // Отображаем TaskDialog, предупреждающий пользователя
                            TaskDialog.Show("Неопределенная марка", "Заполните 'ADSK_марка' в потолках. Один из потолков не имеет определенной марки, что может вызвать ошибки.");
                        }
                    }
                    else
                    {
                        // Отображаем TaskDialog, предупреждающий пользователя
                        TaskDialog.Show("Отсутствуют параметры", "Не удалось найти параметры 'ADSK_марка' или 'DPM_X_Слои.Состав' в типе потолка.");
                    }
                }
                else
                {
                    // Отображаем TaskDialog, предупреждающий пользователя
                    TaskDialog.Show("Отсутствует тип потолка", "У одного из потолков отсутствует тип, что может вызвать ошибки.");
                }
            }

            // Сортируем словарь по ключам (маркам) и формируем итоговую строку
            var sortedDescriptions = typesDescriptions.OrderBy(kvp => kvp.Key);
            StringBuilder typesDescription = new StringBuilder();
            foreach (var kvp in sortedDescriptions)
            {
                typesDescription.AppendLine($"{kvp.Key}:\n{kvp.Value}");
            }

            // Находим параметр помещения и обновляем его многострочным текстом
            Parameter param = room.LookupParameter(RoomCeilingLayerName);
            if (param != null && param.StorageType == StorageType.String)
            {
                param.Set(typesDescription.ToString());
            }
        }
        private void UpdateRoomCeilingLayerArea(Room room, List<Ceiling> ceilings, string parameterName)
        {
            var typesDescriptions = new Dictionary<string, double>();

            foreach (Ceiling ceiling in ceilings)
            {
                // Получаем тип потолка
                ElementType ceilingType = _doc.GetElement(ceiling.GetTypeId()) as ElementType;

                if (ceilingType != null)
                {
                    Parameter markParam = ceilingType.get_Parameter(new Guid("2204049c-d557-4dfc-8d70-13f19715e46d")); // ADSK_Этаж
                    if (markParam == null || string.IsNullOrEmpty(markParam.AsString()))
                    {
                        // Отображаем TaskDialog, предупреждающий пользователя
                        TaskDialog.Show("Неопределенная марка", "Заполните 'ADSK_марка' в потолках. Один из потолков не имеет определенной марки, что может вызвать ошибки.");
                        continue; // Пропускаем этот потолок и продолжаем с остальными
                    }

                    string mark = markParam.AsString();
                    double area = ceiling.get_Parameter(BuiltInParameter.HOST_AREA_COMPUTED).AsDouble() * 0.092903; // Преобразуем площадь в метры квадратные

                    if (!typesDescriptions.ContainsKey(mark))
                    {
                        typesDescriptions[mark] = area;
                    }
                    else
                    {
                        typesDescriptions[mark] += area; // Суммируем площади одинаковых типов потолков
                    }
                }
                else
                {
                    // Отображаем TaskDialog, предупреждающий пользователя
                    TaskDialog.Show("Отсутствует тип потолка", "У одного из потолков отсутствует тип, что может вызвать ошибки.");
                }
            }

            var sortedDescriptions = typesDescriptions.OrderBy(kvp => kvp.Key);
            StringBuilder typesDescription = new StringBuilder();
            foreach (var kvp in sortedDescriptions)
            {
                // Форматируем строку с добавлением "м²" к значению площади
                typesDescription.AppendLine($"{kvp.Key}: {kvp.Value:F2} м²");
            }

            Parameter param = room.LookupParameter(parameterName);
            if (param != null && param.StorageType == StorageType.String)
            {
                param.Set(typesDescription.ToString());
            }
        }
        private void UpdateRoomCeilingHeight(Room room, List<Ceiling> ceilings, string parameterName)
        {
            var heightDescriptions = new Dictionary<string, string>();

            foreach (Ceiling ceiling in ceilings)
            {
                ElementType ceilingType = _doc.GetElement(ceiling.GetTypeId()) as ElementType;

                if (ceilingType != null)
                {
                    Parameter markParam = ceilingType.get_Parameter(new Guid("2204049c-d557-4dfc-8d70-13f19715e46d")); // ADSK_Этаж
                    if (markParam == null || string.IsNullOrEmpty(markParam.AsString()))
                    {
                        // Отображаем TaskDialog, предупреждающий пользователя
                        TaskDialog.Show("Неопределенная марка", "Заполните 'ADSK_марка' в потолках. Один из потолков не имеет определенной марки, что может вызвать ошибки.");
                        continue; // Пропускаем этот потолок и продолжаем с остальными
                    }

                    string mark = markParam.AsString();
                    Parameter heightParam = ceiling.get_Parameter(BuiltInParameter.CEILING_HEIGHTABOVELEVEL_PARAM);
                    if (heightParam != null && heightParam.StorageType == StorageType.Double)
                    {
                        double height = heightParam.AsDouble();
                        // Преобразуем высоту из внутреннего формата Revit (футы) в метры
                        height = UnitUtils.ConvertFromInternalUnits(height, UnitTypeId.Meters);
                        heightDescriptions[mark] = $"{height:F2}м";
                    }
                    else
                    {
                        // Отображаем TaskDialog, предупреждающий пользователя
                        TaskDialog.Show("Отсутствует параметр высоты", "Не удалось найти параметр высоты для потолка.");
                    }
                }
                else
                {
                    // Отображаем TaskDialog, предупреждающий пользователя
                    TaskDialog.Show("Отсутствует тип потолка", "У одного из потолков отсутствует тип, что может вызвать ошибки.");
                }
            }

            var sortedHeights = heightDescriptions.OrderBy(kvp => kvp.Key);
            StringBuilder heightsDescription = new StringBuilder();
            foreach (var kvp in sortedHeights)
            {
                heightsDescription.AppendLine($"{kvp.Key}: {kvp.Value}");
            }

            Parameter param = room.LookupParameter(parameterName);
            if (param != null && param.StorageType == StorageType.String)
            {
                param.Set(heightsDescription.ToString());
            }
        }

        // Walls
        private void UpdateRoomWallLayerName(Room room, List<Wall> finishingMainWalls, string parameterName)
        {
            var typesDescriptions = new Dictionary<string, string>();

            foreach (Wall finishingMainWall in finishingMainWalls)
            {
                WallType wallType = finishingMainWall.WallType;

                if (wallType != null)
                {
                    Parameter markParam = wallType.get_Parameter(new Guid("2204049c-d557-4dfc-8d70-13f19715e46d")); // ADSK_Этаж
                    Parameter compositionParam = wallType.get_Parameter(new Guid("92f979b3-1252-42e1-92aa-b4a9337e285f")); // Состав

                    if (markParam != null && compositionParam != null)
                    {
                        string mark = markParam.AsString();
                        string composition = compositionParam.AsString();

                        if (!string.IsNullOrEmpty(mark)) // Проверяем наличие значения марки
                        {
                            typesDescriptions[mark] = composition;
                        }
                        else
                        {
                            // Если марка не определена, отображаем сообщение
                            TaskDialog.Show("Неопределенная марка", "Не все стены имеют определенную марку 'ADSK_марка'.");
                            continue; // Пропускаем эту стену и продолжаем с остальными
                        }
                    }
                    else
                    {
                        // Если параметры не найдены, отображаем сообщение
                        TaskDialog.Show("Отсутствуют параметры", "Не удалось найти параметры марки или состава в типе стены.");
                        continue; // Пропускаем эту стену и продолжаем с остальными
                    }
                }
                else
                {
                    // Если тип стены не найден, отображаем сообщение
                    TaskDialog.Show("Отсутствует тип стены", "У одной из стен отсутствует тип, что может вызвать ошибки.");
                    continue; // Пропускаем эту стену и продолжаем с остальными
                }
            }

            // Сортируем и формируем итоговую строку
            var sortedDescriptions = typesDescriptions.OrderBy(kvp => kvp.Key);
            StringBuilder typesDescription = new StringBuilder();
            foreach (var kvp in sortedDescriptions)
            {
                typesDescription.AppendLine($"{kvp.Key}:\n{kvp.Value}");
            }

            // Обновляем параметр помещения
            Parameter param = room.LookupParameter(parameterName);
            if (param != null && param.StorageType == StorageType.String)
            {
                param.Set(typesDescription.ToString());
            }
            else
            {
                // Если параметр не найден или его тип хранения не является строкой, отображаем сообщение
                TaskDialog.Show("Ошибка параметра", $"Не удалось обновить параметр '{parameterName}' для помещения с номером '{room.Number}'.");
            }
        }
        private void UpdateRoomWallLayerArea(Room room, List<Wall> finishingMainWalls, string parameterName)
        {
            var typesDescriptions = new Dictionary<string, double>();

            foreach (Wall finishingMainWall in finishingMainWalls)
            {
                Parameter markParam = finishingMainWall.WallType.get_Parameter(new Guid("2204049c-d557-4dfc-8d70-13f19715e46d")); // ADSK_Этаж
                if (markParam == null || string.IsNullOrEmpty(markParam.AsString()))
                {
                    // Отображаем TaskDialog, предупреждающий пользователя
                    TaskDialog dialog = new TaskDialog("Неопределенная марка")
                    {
                        MainInstruction = "Заполните 'ADSK_марка' в стенах",
                        MainContent = "Один из полов не имеет определенной марки, что может вызвать ошибки.",
                        CommonButtons = TaskDialogCommonButtons.Close,
                        DefaultButton = TaskDialogResult.Close
                    };
                    dialog.Show();
                    continue; // Пропускаем этот пол и продолжаем с остальными
                }

                string mark = markParam.AsString();
                double area = finishingMainWall.get_Parameter(BuiltInParameter.HOST_AREA_COMPUTED).AsDouble() * 0.092903; // Преобразуем площадь в метры квадратные

                if (!typesDescriptions.ContainsKey(mark))
                {
                    typesDescriptions[mark] = area;
                }
                else
                {
                    typesDescriptions[mark] += area; // Суммируем площади одинаковых типов полов
                }
            }

            var sortedDescriptions = typesDescriptions.OrderBy(kvp => kvp.Key);
            StringBuilder typesDescription = new StringBuilder();
            foreach (var kvp in sortedDescriptions)
            {
                // Форматируем строку с добавлением "м²" к значению площади
                typesDescription.AppendLine($"{kvp.Key}: {kvp.Value:F2} м²");
            }

            Parameter param = room.LookupParameter(parameterName);
            if (param != null && param.StorageType == StorageType.String)
            {
                param.Set(typesDescription.ToString());
            }
        }
        // Columns
        private void UpdateRoomColumnLayerName(Room room, List<Wall> finishingColumnWalls, string parameterName)
        {
            var typesDescriptions = new Dictionary<string, string>();

            foreach (Wall finishingColumnWall in finishingColumnWalls)
            {
                WallType wallType = finishingColumnWall.WallType;

                if (wallType != null)
                {
                    Parameter markParam = wallType.get_Parameter(new Guid("2204049c-d557-4dfc-8d70-13f19715e46d")); // ADSK_Этаж
                    Parameter compositionParam = wallType.get_Parameter(new Guid("92f979b3-1252-42e1-92aa-b4a9337e285f")); // Состав

                    if (markParam != null && compositionParam != null)
                    {
                        string mark = markParam.AsString();
                        string composition = compositionParam.AsString();

                        if (!string.IsNullOrEmpty(mark)) // Проверяем наличие значения марки
                        {
                            typesDescriptions[mark] = composition;
                        }
                        else
                        {
                            // Если марка не определена, отображаем сообщение
                            TaskDialog.Show("Неопределенная марка", "Не все стены колонн имеют определенную марку 'ADSK_марка'.");
                            continue; // Пропускаем эту стену и продолжаем с остальными
                        }
                    }
                    else
                    {
                        // Если параметры не найдены, отображаем сообщение
                        TaskDialog.Show("Отсутствуют параметры", "Не удалось найти параметры марки или состава в типе стены колонн.");
                        continue; // Пропускаем эту стену и продолжаем с остальными
                    }
                }
                else
                {
                    // Если тип стены не найден, отображаем сообщение
                    TaskDialog.Show("Отсутствует тип стены колонн", "У одной из стен колонн отсутствует тип, что может вызвать ошибки.");
                    continue; // Пропускаем эту стену и продолжаем с остальными
                }
            }

            // Сортируем и формируем итоговую строку
            var sortedDescriptions = typesDescriptions.OrderBy(kvp => kvp.Key);
            StringBuilder typesDescription = new StringBuilder();
            foreach (var kvp in sortedDescriptions)
            {
                typesDescription.AppendLine($"{kvp.Key}:\n{kvp.Value}");
            }

            // Обновляем параметр помещения
            Parameter param = room.LookupParameter(parameterName);
            if (param != null && param.StorageType == StorageType.String)
            {
                param.Set(typesDescription.ToString());
            }
            else
            {
                // Если параметр не найден или его тип хранения не является строкой, отображаем сообщение
                TaskDialog.Show("Ошибка параметра", $"Не удалось обновить параметр '{parameterName}' для помещения с номером '{room.Number}'.");
            }
        }
        private void UpdateRoomColumnLayerArea(Room room, List<Wall> finishingColumnWalls, string parameterName)
        {
            var typesDescriptions = new Dictionary<string, double>();

            foreach (Wall finishingColumnWall in finishingColumnWalls)
            {
                Parameter markParam = finishingColumnWall.WallType.get_Parameter(new Guid("2204049c-d557-4dfc-8d70-13f19715e46d")); // ADSK_Этаж
                if (markParam == null || string.IsNullOrEmpty(markParam.AsString()))
                {
                    // Отображаем TaskDialog, предупреждающий пользователя
                    TaskDialog dialog = new TaskDialog("Неопределенная марка")
                    {
                        MainInstruction = "Заполните 'ADSK_марка' в стенах колонн",
                        MainContent = "Одна из стен колонн не имеет определенной марки, что может вызвать ошибки.",
                        CommonButtons = TaskDialogCommonButtons.Close,
                        DefaultButton = TaskDialogResult.Close
                    };
                    dialog.Show();
                    continue; // Пропускаем этот пол и продолжаем с остальными
                }

                string mark = markParam.AsString();
                double area = finishingColumnWall.get_Parameter(BuiltInParameter.HOST_AREA_COMPUTED).AsDouble() * 0.092903; // Преобразуем площадь в метры квадратные

                if (!typesDescriptions.ContainsKey(mark))
                {
                    typesDescriptions[mark] = area;
                }
                else
                {
                    typesDescriptions[mark] += area; // Суммируем площади одинаковых типов полов
                }
            }

            var sortedDescriptions = typesDescriptions.OrderBy(kvp => kvp.Key);
            StringBuilder typesDescription = new StringBuilder();
            foreach (var kvp in sortedDescriptions)
            {
                // Форматируем строку с добавлением "м²" к значению площади
                typesDescription.AppendLine($"{kvp.Key}: {kvp.Value:F2} м²");
            }

            Parameter param = room.LookupParameter(parameterName);
            if (param != null && param.StorageType == StorageType.String)
            {
                param.Set(typesDescription.ToString());
            }
        }
        // Columns
        private void UpdateRoomSkirtLayerName(Room room, List<Wall> finishingSkirtWalls, string parameterName)
        {
            var typesDescriptions = new Dictionary<string, string>();

            foreach (Wall finishingSkirtWall in finishingSkirtWalls)
            {
                WallType wallType = finishingSkirtWall.WallType;

                if (wallType != null)
                {
                    Parameter markParam = wallType.get_Parameter(new Guid("2204049c-d557-4dfc-8d70-13f19715e46d")); // ADSK_Этаж
                    Parameter compositionParam = wallType.get_Parameter(new Guid("92f979b3-1252-42e1-92aa-b4a9337e285f")); // Состав

                    if (markParam != null && compositionParam != null)
                    {
                        string mark = markParam.AsString();
                        string composition = compositionParam.AsString();

                        if (!string.IsNullOrEmpty(mark)) // Проверяем наличие значения марки
                        {
                            typesDescriptions[mark] = composition;
                        }
                        else
                        {
                            // Если марка не определена, отображаем сообщение
                            TaskDialog.Show("Неопределенная марка", "Не все плинтусы имеют определенную марку 'ADSK_марка'.");
                            continue; // Пропускаем эту стену и продолжаем с остальными
                        }
                    }
                    else
                    {
                        // Если параметры не найдены, отображаем сообщение
                        TaskDialog.Show("Отсутствуют параметры", "Не удалось найти параметры марки или состава в типе плинтусы.");
                        continue; // Пропускаем эту стену и продолжаем с остальными
                    }
                }
                else
                {
                    // Если тип стены не найден, отображаем сообщение
                    TaskDialog.Show("Отсутствует тип плинтусы", "У одной из стен колонн отсутствует тип, что может вызвать ошибки.");
                    continue; // Пропускаем эту стену и продолжаем с остальными
                }
            }

            // Сортируем и формируем итоговую строку
            var sortedDescriptions = typesDescriptions.OrderBy(kvp => kvp.Key);
            StringBuilder typesDescription = new StringBuilder();
            foreach (var kvp in sortedDescriptions)
            {
                typesDescription.AppendLine($"{kvp.Key}:\n{kvp.Value}");
            }

            // Обновляем параметр помещения
            Parameter param = room.LookupParameter(parameterName);
            if (param != null && param.StorageType == StorageType.String)
            {
                param.Set(typesDescription.ToString());
            }
            else
            {
                // Если параметр не найден или его тип хранения не является строкой, отображаем сообщение
                TaskDialog.Show("Ошибка параметра", $"Не удалось обновить параметр '{parameterName}' для помещения с номером '{room.Number}'.");
            }
        }
        private void UpdateRoomSkirtLayerLength(Room room, List<Wall> finishingSkirtWalls, string parameterName)
        {
            var typesDescriptions = new Dictionary<string, double>();

            foreach (Wall finishingSkirtWall in finishingSkirtWalls)
            {
                Parameter markParam = finishingSkirtWall.WallType.get_Parameter(new Guid("2204049c-d557-4dfc-8d70-13f19715e46d")); // ADSK_Этаж
                if (markParam == null || string.IsNullOrEmpty(markParam.AsString()))
                {
                    // Отображаем TaskDialog, предупреждающий пользователя
                    TaskDialog dialog = new TaskDialog("Неопределенная марка")
                    {
                        MainInstruction = "Заполните 'ADSK_марка' в плинтусах",
                        MainContent = "Один из типов плинтусов не имеет определенной марки, что может вызвать ошибки.",
                        CommonButtons = TaskDialogCommonButtons.Close,
                        DefaultButton = TaskDialogResult.Close
                    };
                    dialog.Show();
                    continue; // Пропускаем этот пол и продолжаем с остальными
                }

                string mark = markParam.AsString();
                double length = (finishingSkirtWall.get_Parameter(BuiltInParameter.CURVE_ELEM_LENGTH).AsDouble() * 304.8) / 1000; // Преобразуем длину

                if (!typesDescriptions.ContainsKey(mark))
                {
                    typesDescriptions[mark] = length;
                }
                else
                {
                    typesDescriptions[mark] += length; // Суммируем длины одинаковых типов 
                }
            }

            var sortedDescriptions = typesDescriptions.OrderBy(kvp => kvp.Key);
            StringBuilder typesDescription = new StringBuilder();
            foreach (var kvp in sortedDescriptions)
            {
                // Форматируем строку с добавлением "м²" к значению площади
                typesDescription.AppendLine($"{kvp.Key}: {kvp.Value:F2} м.п.");
            }

            Parameter param = room.LookupParameter(parameterName);
            if (param != null && param.StorageType == StorageType.String)
            {
                param.Set(typesDescription.ToString());
            }
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

        // Walls
        private bool IsWallWithRoomNumber(Wall wall, string roomNumber)
        {
            // Получаем значение параметра "Номер" для данного пола
            Parameter roomNumberParam = wall.LookupParameter(RoomNumberParam);

            // Проверяем, содержит ли параметр заданное значение
            string wallRoomNumber = roomNumberParam != null ? roomNumberParam.AsString() : string.Empty;
            return wallRoomNumber == roomNumber;
        }


    }
}
