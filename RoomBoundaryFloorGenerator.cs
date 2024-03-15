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
        public void FloorCutDoor()
        {
            using (Transaction trans = new Transaction(_doc, "Modify Floors for Door Thresholds"))
            {
                trans.Start();

                foreach (Floor floor in createdFloors)
                {
                    ElementId floorId = floor.Id;
                    if (floorDoorIntersections.TryGetValue(floorId, out List<ElementId> intersectingDoors))
                    {
                        using (FloorEditScope floorEditScope = new FloorEditScope(_doc, "Edit Floor Boundary"))
                        {
                            floorEditScope.Start(floorId);

                            foreach (ElementId doorId in intersectingDoors)
                            {
                                FamilyInstance door = _doc.GetElement(doorId) as FamilyInstance;
                                LocationPoint doorLocation = door.Location as LocationPoint;

                                if (doorLocation != null)
                                {
                                    // Получаем трансформацию для двери (учитываем положение и ориентацию)
                                    Transform doorTransform = doorLocation.Point is XYZ point ? Transform.CreateTranslation(point - XYZ.Zero) : Transform.Identity;
                                    XYZ doorDirection = door.FacingOrientation;
                                    XYZ doorNormal = doorTransform.OfVector(doorDirection.CrossProduct(XYZ.BasisZ));

                                    // Получаем ширину двери
                                    Parameter doorWidthParam = door.get_Parameter(BuiltInParameter.DOOR_WIDTH);
                                    double doorWidth = doorWidthParam.AsDouble();

                                    // Вычисляем половину ширины двери для смещения
                                    double halfDoorWidth = doorWidth / 2;

                                    // Получаем границы текущего пола
                                    EdgeArrayArray floorEdgeArrayArray = floor.GetEdgesAsCurveLoops();
                                    CurveLoop largestCurveLoop = GetLargestCurveLoop(floorEdgeArrayArray);

                                    // Редактируем границы пола, добавляя сегменты
                                    CurveLoop newCurveLoop = CurveLoop.CreateViaOffset(largestCurveLoop, halfDoorWidth, doorNormal);

                                    // Удаляем исходные сегменты пола, которые попадают внутрь нового контура
                                    RemoveRedundantSegments(largestCurveLoop, newCurveLoop);

                                    // Создаем новые отрезки для объединения с основным контуром пола
                                    ConnectNewAndOldSegments(largestCurveLoop, newCurveLoop);

                                    // Применяем изменения к границам пола
                                    floorEditScope.PerformCommit(newCurveLoop);
                                }
                            }

                            floorEditScope.Commit(_doc);
                        }
                    }
                }

                trans.Commit();
            }
        }

        // Вспомогательные методы

        CurveLoop GetLargestCurveLoop(EdgeArrayArray edgeArrayArray)
        {
            CurveLoop largestLoop = null;
            double maxArea = 0.0;

            foreach (EdgeArray edgeArray in edgeArrayArray)
            {
                CurveLoop curveLoop = new CurveLoop();
                foreach (Edge edge in edgeArray)
                {
                    curveLoop.Append(edge.AsCurve());
                }

                double currentArea = GetCurveLoopArea(curveLoop);
                if (currentArea > maxArea)
                {
                    largestLoop = curveLoop;
                    maxArea = currentArea;
                }
            }

            return largestLoop;
        }

        void RemoveRedundantSegments(CurveLoop existingLoop, CurveLoop newLoop)
        {
            // Реализация логики удаления лишних отрезков
        }

        void ConnectNewAndOldSegments(CurveLoop existingLoop, CurveLoop newLoop)
        {
            // Реализация логики соединения новых и старых отрезков
        }

        double GetCurveLoopArea(CurveLoop curveLoop)
        {
            // Реализация расчета площади CurveLoop
        }






        // TODO: Implement the details of each TODO step.


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