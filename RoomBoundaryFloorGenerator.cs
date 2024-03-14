using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;
using Autodesk.Revit.UI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Media;

namespace AR_Finishings
{
    public class RoomBoundaryFloorGenerator
    {
        private Document _doc;
        private List<Floor> createdFloors;
        private Dictionary<ElementId, List<ElementId>> floorDoorIntersections = new Dictionary<ElementId, List<ElementId>>();
        private Dictionary<ElementId, IList<IList<BoundarySegment>>> createdCurves = new Dictionary<ElementId, IList<IList<BoundarySegment>>>();

        public RoomBoundaryFloorGenerator(Document doc)
        {
            _doc = doc;
            createdFloors = new List<Floor>();
            floorDoorIntersections = new Dictionary<ElementId, List<ElementId>>();
            createdCurves = new Dictionary<ElementId, IList<IList<BoundarySegment>>>();

        }

        public void CreateFloors(IEnumerable<ElementId> selectedRoomIds, FloorType selectedFloorType)
        {
            StringBuilder message = new StringBuilder("Generated Floors for Room IDs:\n");
            using (Transaction trans = new Transaction(_doc, "Generate Floors"))
            {
                trans.Start();
                foreach (ElementId roomId in selectedRoomIds)
                {
                    Room room = _doc.GetElement(roomId) as Room;
                    string roomNameValue = room.get_Parameter(BuiltInParameter.ROOM_NAME).AsString();
                    string roomNumberValue = room.get_Parameter(BuiltInParameter.ROOM_NUMBER).AsString();
                    string levelRoomStringValue = room.get_Parameter(BuiltInParameter.LEVEL_NAME).AsString().Split('_')[1];
                    if (room != null)
                    {
                        Level level = _doc.GetElement(room.LevelId) as Level;
                        if (level != null)
                        {
                            double roomLowerOffset = room.get_Parameter(BuiltInParameter.ROOM_LOWER_OFFSET).AsDouble();
                            SpatialElementBoundaryOptions options = new SpatialElementBoundaryOptions();
                            IList<IList<BoundarySegment>> boundaries = room.GetBoundarySegments(options);

                            if (boundaries.Count > 0)
                            {
                                // Основной контур пола
                                CurveLoop mainCurveLoop = CurveLoop.Create(boundaries[0].Select(seg => seg.GetCurve()).ToList());
                                IList<CurveLoop> loops = new List<CurveLoop> { mainCurveLoop };

                                // Внутренние контуры (отверстия)
                                for (int i = 1; i < boundaries.Count; i++)
                                {
                                    CurveLoop innerCurveLoop = CurveLoop.Create(boundaries[i].Select(seg => seg.GetCurve()).ToList());
                                    loops.Add(innerCurveLoop); // Добавляем как отверстия в полу
                                }

                                if (loops.Count > 0)
                                {
                                    Floor createdFloor = Floor.Create(_doc, loops, selectedFloorType.Id, level.Id);
                                    createdFloor.get_Parameter(BuiltInParameter.FLOOR_HEIGHTABOVELEVEL_PARAM).Set(roomLowerOffset);
                                    createdFloors.Add(createdFloor);
                                    // Получаем границы основного пола и сохраняем их в словарь
                                    IList<IList<BoundarySegment>> createdBoundaries = room.GetBoundarySegments(new SpatialElementBoundaryOptions());
                                    createdCurves.Add(createdFloor.Id, createdBoundaries);

                                    // Setting parameters
                                    SetupFloorParameters(createdFloor, roomNameValue, roomNumberValue, levelRoomStringValue);

                                    
                                }

                            }
                        }
                    }
                }
                trans.Commit();
            }
        }

        public void CheckFloorsAndDoorsIntersection()
        {
            using (Transaction trans = new Transaction(_doc, "Check Floors and Doors Intersection"))
            {
                trans.Start();

                
                floorDoorIntersections = new Dictionary<ElementId, List<ElementId>>();
                foreach (Floor floor in createdFloors)
                {
                    BoundingBoxXYZ floorBox = floor.get_BoundingBox(null);
                    if (floorBox == null || floorBox.Min.IsAlmostEqualTo(floorBox.Max))
                    {
                        continue;
                    }

                    // Устанавливаем смещение только по координате Z
                    double offset = 200 / 304.8; // Преобразуем миллиметры в футы
                    XYZ minPoint = floorBox.Min - new XYZ(0, 0, offset);
                    XYZ maxPoint = floorBox.Max + new XYZ(0, 0, offset);

                    Outline floorOutline = new Outline(minPoint, maxPoint);
                    BoundingBoxIntersectsFilter filter = new BoundingBoxIntersectsFilter(floorOutline);
                    FilteredElementCollector collector = new FilteredElementCollector(_doc)
                        .OfCategory(BuiltInCategory.OST_Doors)
                        .WherePasses(filter);

                    List<ElementId> intersectingDoors = new List<ElementId>();
                    foreach (Element door in collector)
                    {
                        intersectingDoors.Add(door.Id);
                    }

                    if (intersectingDoors.Count > 0)
                    {
                        floorDoorIntersections.Add(floor.Id, intersectingDoors);
                    }
                }

                StringBuilder sb = new StringBuilder();
                foreach (var kvp in floorDoorIntersections)
                {
                    foreach (var doorId in kvp.Value)
                    {
                        sb.AppendLine($"Floor ID: {kvp.Key.Value} : Door ID: {doorId.Value}");
                    }
                }

                if (sb.Length > 0)
                {
                    TaskDialog.Show("Intersections", sb.ToString());
                }
                else
                {
                    TaskDialog.Show("Intersections", "No intersections found.");
                }

                trans.Commit();
            }
        }
        public void FloorCutDoor(double doorThickness)
        {
            // Проверяем, есть ли какие-либо данные о пересечениях пола и дверей
            if (floorDoorIntersections == null || floorDoorIntersections.Count == 0)
            {
                TaskDialog.Show("Error", "No floor-door intersections found.");
                return;
            }

            // Проходимся по каждому полу в списке пересечений
            foreach (var kvp in floorDoorIntersections)
            {
                Floor originalFloor = _doc.GetElement(kvp.Key) as Floor;
                if (originalFloor == null)
                {
                    TaskDialog.Show("Error", "Original floor not found.");
                    continue;
                }

                // Проверяем, есть ли данные о границах основного пола
                if (!createdCurves.ContainsKey(originalFloor.Id))
                {
                    TaskDialog.Show("Error", "No boundary segments found for the specified floor.");
                    continue;
                }

                // Получаем данные о границах основного пола из сохраненных данных
                IList<IList<BoundarySegment>> boundaries = createdCurves[originalFloor.Id];

                // Получаем список дверей, пересекающихся с данным полом
                List<ElementId> intersectingDoors = kvp.Value;

                // Проходимся по каждой двери и выполняем вырез пола
                foreach (ElementId doorId in intersectingDoors)
                {
                    Element door = _doc.GetElement(doorId);
                    if (door == null || !(door is FamilyInstance)) // Проверяем, что элемент является экземпляром семейства (дверью)
                        continue;

                    FamilyInstance doorInstance = door as FamilyInstance;
                    if (doorInstance == null)
                        continue;

                    // Получаем толщину стены, к которой привязана дверь
                    Parameter hostParam = doorInstance.get_Parameter(BuiltInParameter.HOST_ID_PARAM);
                    Element hostWall = _doc.GetElement(hostParam.AsElementId());
                    double wallThickness = hostWall.get_Parameter(BuiltInParameter.WALL_ATTR_WIDTH_PARAM).AsDouble() / 2;

                    // Получаем центральную линию двери
                    LocationCurve doorLocation = doorInstance.Location as LocationCurve;
                    if (doorLocation == null)
                        continue;

                    Curve doorCurve = doorLocation.Curve;

                    // Получаем центральную точку двери
                    XYZ doorCenter = (doorCurve.GetEndPoint(0) + doorCurve.GetEndPoint(1)) / 2;

                    // Проверяем, что у пола есть границы
                    if (boundaries != null && boundaries.Count > 0)
                    {
                        // Создаем новый контур пола с учетом выреза
                        List<Curve> newBoundarySegments = new List<Curve>();
                        foreach (IList<BoundarySegment> segments in boundaries)
                        {
                            foreach (BoundarySegment segment in segments)
                            {
                                Curve segmentCurve = segment.GetCurve();

                                // Находим точку пересечения центральной линии двери с контуром пола
                                IntersectionResult intersectionResult = segmentCurve.Project(doorCenter);
                                if (intersectionResult != null && intersectionResult.XYZPoint != null)
                                {
                                    XYZ intersectionPoint = intersectionResult.XYZPoint;

                                    // Добавляем сегменты границы до точки пересечения
                                    if (segmentCurve.GetEndPoint(0).DistanceTo(intersectionPoint) <= 0)
                                    {
                                        newBoundarySegments.Add(segmentCurve);
                                    }
                                    // Добавляем отрезок после точки пересечения
                                    else
                                    {
                                        // Создаем новый отрезок от точки пересечения до конечной точки сегмента
                                        Line line = Line.CreateBound(intersectionPoint, segmentCurve.GetEndPoint(1));
                                        newBoundarySegments.Add(line);
                                    }
                                }
                                else
                                {
                                    // Если точка пересечения не найдена, просто добавляем текущий сегмент
                                    newBoundarySegments.Add(segmentCurve);
                                }
                            }
                        }

                        // Создаем новый контур пола
                        CurveLoop newBoundary = CurveLoop.Create(newBoundarySegments);
                        List<CurveLoop> loops = new List<CurveLoop> { newBoundary };

                        // Получаем параметры исходного пола
                        FloorType originalFloorType = _doc.GetElement(originalFloor.FloorType.Id) as FloorType;
                        Level originalLevel = _doc.GetElement(originalFloor.LevelId) as Level;

                        // Создаем новый пол с учетом выреза
                        Floor createdFloor = Floor.Create(_doc, loops, originalFloorType.Id, originalLevel.Id);
                        createdFloor.get_Parameter(BuiltInParameter.FLOOR_HEIGHTABOVELEVEL_PARAM).Set(originalFloor.get_Parameter(BuiltInParameter.FLOOR_HEIGHTABOVELEVEL_PARAM).AsDouble());

                        // Копируем общие параметры из исходного пола
                        CopyParameters(originalFloor, createdFloor);
                    }
                }
            }
        }







        private void CopyParameters(Floor source, Floor target)
        {
            // Копируем параметры из одного элемента в другой
            foreach (Parameter param in source.Parameters)
            {
                if (param.IsReadOnly || param.StorageType == StorageType.ElementId)
                    continue;

                Parameter targetParam = target.get_Parameter(param.Definition);
                if (targetParam != null && targetParam.StorageType == param.StorageType)
                {
                    switch (param.StorageType)
                    {
                        case StorageType.Double:
                            targetParam.Set(param.AsDouble());
                            break;
                        case StorageType.Integer:
                            targetParam.Set(param.AsInteger());
                            break;
                        case StorageType.String:
                            targetParam.Set(param.AsString());
                            break;
                    }
                }
            }
        }



        private void SetupFloorParameters(Floor floor, string roomNameValue, string roomNumberValue, string levelRoomStringValue)
        {

            // Пример установки значения общего параметра (предполагая, что параметр уже добавлен в проект)
            Guid roomNameGuid = new Guid("4a5cec5d-f883-42c3-a05c-89ec822d637b"); // GUID общего параметра
            Parameter roomNameParam = floor.get_Parameter(roomNameGuid);
            if (roomNameParam != null && roomNameParam.StorageType == StorageType.String)
            {
                roomNameParam.Set(roomNameValue); // Установка значения параметра
            }
            // Пример установки значения общего параметра (предполагая, что параметр уже добавлен в проект)
            Guid roomNumberGuid = new Guid("317bbea6-a1a8-4923-a722-635c998c184d"); // GUID общего параметра
            Parameter roomNumberParam = floor.get_Parameter(roomNumberGuid);
            if (roomNumberParam != null && roomNumberParam.StorageType == StorageType.String)
            {
                roomNumberParam.Set(roomNumberValue); // Установка значения параметра
            }
            // Пример установки значения общего параметра (предполагая, что параметр уже добавлен в проект)
            Guid levelGuid = new Guid("9eabf56c-a6cd-4b5c-a9d0-e9223e19ea3f"); // GUID общего параметра
            Parameter floorLevelParam = floor.get_Parameter(levelGuid);
            if (floorLevelParam != null && floorLevelParam.StorageType == StorageType.String)
            {
                floorLevelParam.Set(levelRoomStringValue); // Установка значения параметра
            }

        }
    }
}
