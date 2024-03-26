
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;
using Autodesk.Revit.UI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Media;
using Color = Autodesk.Revit.DB.Color;
using Transform = Autodesk.Revit.DB.Transform;

namespace AR_Finishings
{
    public class RoomBoundaryFloorGenerator
    {
        private Document _doc;
        private double _doorDepth;
        private double doorWidth;
        public double DoorDepth { get; set; }
        private List<Floor> createdFloors;
        private Dictionary<ElementId, List<ElementId>> floorDoorIntersections = new Dictionary<ElementId, List<ElementId>>();
        private Dictionary<ElementId, IList<IList<BoundarySegment>>> createdCurves = new Dictionary<ElementId, IList<IList<BoundarySegment>>>();
        private List<ModelCurve> phantomLines;


        public RoomBoundaryFloorGenerator(Document doc, double doorDepth)
        {
            _doc = doc;
            createdFloors = new List<Floor>();
            phantomLines = new List<ModelCurve>();
            createdCurves = new Dictionary<ElementId, IList<IList<BoundarySegment>>>();
            _doorDepth = doorDepth;



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
            using (Transaction trans = new Transaction(_doc, "Visualize Door Thresholds"))
            {
                trans.Start();

                foreach (var kvp in floorDoorIntersections)
                {
                    Floor floor = _doc.GetElement(kvp.Key) as Floor;
                    if (floor != null)
                    {
                        foreach (ElementId doorId in kvp.Value)
                        {
                            FamilyInstance door = _doc.GetElement(doorId) as FamilyInstance;
                            if (door != null && door.Host is Wall hostWall)
                            {
                                double wallWidth = hostWall.Width;
                                LocationPoint doorLocation = door.Location as LocationPoint;
                                if (doorLocation != null)
                                {
                                    XYZ doorCenter = doorLocation.Point;
                                    XYZ doorOrientation = door.HandOrientation;
                                    XYZ wallDirection = hostWall.Orientation.Normalize();

                                    ElementType doorType = _doc.GetElement(door.GetTypeId()) as ElementType;
                                    if (doorType != null)
                                    {
                                        Parameter doorWidthParam = doorType.get_Parameter(BuiltInParameter.DOOR_WIDTH);
                                        if (doorWidthParam != null)
                                        {
                                            double doorWidth = doorWidthParam.AsDouble();
                                            double halfDoorWidth = doorWidth / 2.0;

                                            // Определяем направление открытия двери относительно стены
                                            bool isOutwardOpening = doorOrientation.CrossProduct(wallDirection).IsAlmostEqualTo(XYZ.BasisZ);

                                            // Определяем глубину вырезания в зависимости от выбранной опции
                                            double cutDepth = 0.0; // Инициализируем с нулевой глубиной
                                            if (DoorDepth == 0.5)
                                            {
                                                cutDepth = wallWidth / 4.0; // Половина ширины стены
                                            }
                                            else if (DoorDepth == 1)
                                            {
                                                cutDepth = wallWidth / 2.0; // Полная ширина стены
                                            }

                                            // Вычисляем левый и правый край двери
                                            XYZ leftEdge = doorCenter - wallDirection * halfDoorWidth;
                                            XYZ rightEdge = doorCenter + wallDirection * halfDoorWidth;

                                            // Смещаем левый и правый край двери на глубину вырезания
                                            XYZ startCut = leftEdge - wallDirection;
                                            XYZ endCut = rightEdge + wallDirection;

                                            // Если дверь открывается наружу, меняем направление отрезка
                                            if (isOutwardOpening)
                                            {
                                                XYZ temp = startCut;
                                                startCut = endCut;
                                                endCut = temp;
                                            }

                                            // Создание модельной линии на основе вычисленных точек
                                            SketchPlane sketchPlane = SketchPlane.Create(_doc, Plane.CreateByNormalAndOrigin(XYZ.BasisZ, doorCenter));
                                            ModelCurve modelCurve = CreateModelLine(startCut, endCut, sketchPlane);

                                            // Добавляем созданную модельную линию в список phantomLines
                                            phantomLines.Add(modelCurve);

                                            // Вызываем метод для создания новых точек старта и конца
                                            NewStartPoints(cutDepth, doorWidth);
                                        }
                                    }
                                }
                            }
                        }
                    }
                }

                trans.Commit();
            }
        }


        private ModelCurve CreateModelLine(XYZ start, XYZ end, SketchPlane sketchPlane)
        {
            // Условие для создания линии в зависимости от выбранной глубины
            if (start.DistanceTo(end) > _doc.Application.ShortCurveTolerance && DoorDepth != 0)
            {
                Line line = Line.CreateBound(start, end);
                return _doc.Create.NewModelCurve(line, sketchPlane) as ModelCurve;
            }
            return null;
        }

        private void NewStartPoints(double cutDepth, double doorWidth)
        {
            List<ModelCurve> newPhantomLines = new List<ModelCurve>();
            List<ModelCurve> upperLines = new List<ModelCurve>();

            foreach (ModelCurve phantomLine in phantomLines)
            {
                foreach (var curveKvp in createdCurves)
                {
                    foreach (IList<BoundarySegment> boundarySegments in curveKvp.Value)
                    {
                        foreach (BoundarySegment boundarySegment in boundarySegments)
                        {
                            IntersectionResultArray intersectionResultArray;
                            SetComparisonResult result = boundarySegment.GetCurve().Intersect(phantomLine.GeometryCurve, out intersectionResultArray);
                            if (result == SetComparisonResult.Overlap)
                            {
                                foreach (IntersectionResult intersectionResult in intersectionResultArray)
                                {
                                    XYZ intersectionPoint = intersectionResult.XYZPoint;

                                    XYZ tangent = boundarySegment.GetCurve().ComputeDerivatives(0.5, true).BasisX.Normalize();
                                    XYZ perpendicular = new XYZ(-tangent.Y, tangent.X, tangent.Z);
                                    XYZ wallDirection = boundarySegment.GetCurve().ComputeDerivatives(0.5, true).BasisY.Normalize();

                                    // Изменяем конечную точку линии на правую сторону двери
                                    XYZ startCut = intersectionPoint - perpendicular * cutDepth * 2;
                                    XYZ endCut = intersectionPoint + wallDirection * doorWidth / 2.0;

                                    ModelCurve newModelCurve = CreateModelLine(startCut, endCut, phantomLine.SketchPlane);
                                    newPhantomLines.Add(newModelCurve);
                                    ChangeColor(newModelCurve, new Autodesk.Revit.DB.Color(255, 0, 0));

                                    // Создаем точки для верхней линии
                                    XYZ startUpPoint = startCut + perpendicular * doorWidth / 2.0;
                                    XYZ endUpPoint = startCut - perpendicular * doorWidth / 2.0;

                                    // Находим середину желтого отрезка
                                    XYZ midYellowPoint = (startUpPoint + endUpPoint) / 2.0;

                                    // Находим вектор, соединяющий начальную и конечную точки желтого отрезка
                                    XYZ yellowVector = (endUpPoint - startUpPoint).Normalize();

                                    // Поворачиваем вектор на 90 градусов по часовой стрелке
                                    XYZ rotatedDirection = new XYZ(-yellowVector.Y, yellowVector.X, yellowVector.Z);

                                    // Создаем точки для верхней линии, учитывая поворот на 90 градусов по часовой стрелке
                                    XYZ startUpPointNew = midYellowPoint + rotatedDirection * doorWidth / 2.0;
                                    XYZ endUpPointNew = midYellowPoint - rotatedDirection * doorWidth / 2.0;

                                    ModelCurve newModelUpCurve = CreateModelLine(startUpPointNew, endUpPointNew, phantomLine.SketchPlane);
                                    upperLines.Add(newModelUpCurve);
                                    ChangeColor(newModelUpCurve, new Autodesk.Revit.DB.Color(255, 255, 0));
                                }
                            }
                        }
                    }
                }
            }

            // Удаляем старые отрезки из списка
            foreach (ModelCurve modelCurve in phantomLines)
            {
                _doc.Delete(modelCurve.Id);
            }

            // Заменяем старый список смещенными отрезками
            phantomLines = newPhantomLines;
        }


        // Метод для изменения цвета модельной кривой
        private void ChangeColor(ModelCurve modelCurve, Autodesk.Revit.DB.Color color)
        {
            OverrideGraphicSettings overrideSettings = new OverrideGraphicSettings();
            overrideSettings.SetProjectionLineColor(color);

            _doc.ActiveView.SetElementOverrides(modelCurve.Id, overrideSettings);
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
