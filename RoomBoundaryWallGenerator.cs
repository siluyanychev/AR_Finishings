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
    public class RoomBoundaryWallGenerator
    {
        private Document _doc;
        private double _wallHeight;

        public RoomBoundaryWallGenerator(Document doc, double wallHeight)
        {
            _doc = doc;
            _wallHeight = wallHeight;
        }
        public void CreateWalls(IEnumerable<ElementId> selectedRoomIds, WallType selectedWallType)
        {
            StringBuilder message = new StringBuilder("Generated Walls for Room IDs:\n");
            using (Transaction trans = new Transaction(_doc, "Generate Walls"))

            {
                List<Wall> createdWalls = new List<Wall>();

                trans.Start();
                foreach (ElementId roomId in selectedRoomIds)
                {
                    Room room = _doc.GetElement(roomId) as Room;
                    Level level = _doc.GetElement(room.LevelId) as Level;
                    double roomLowerOffset = room.get_Parameter(BuiltInParameter.ROOM_LOWER_OFFSET).AsDouble();

                    var boundaries = room.GetBoundarySegments(new SpatialElementBoundaryOptions());
                    foreach (var boundary in boundaries)
                    {
                        foreach (var segment in boundary)
                        {
                            Element boundaryElement = _doc.GetElement(segment.ElementId);

                            // Проверяем, является ли элемент стеной с CurtainGrid
                            Wall boundaryWall = boundaryElement as Wall;
                            if (boundaryWall != null && boundaryWall.CurtainGrid != null)
                            {
                                // Пропускаем создание стены, если это витраж
                                continue;
                            }

                            // Проверяем, является ли элемент разделителем помещений
                            if (boundaryElement.Category != null &&
                                boundaryElement.Category.Id.Value == (int)BuiltInCategory.OST_RoomSeparationLines)
                            {
                                continue;
                            }
                            Curve curve = segment.GetCurve();
                            // Для кривых, которые представляют внешние края стен
                            Curve outerCurve = curve.CreateOffset(selectedWallType.Width / -2.0, XYZ.BasisZ);
                            // Для кривых, которые представляют внутренние края стен
                            Curve innerCurve = curve.CreateOffset(selectedWallType.Width / 2.0, XYZ.BasisZ);

                            Wall createdWall = Wall.Create(_doc, outerCurve, selectedWallType.Id, level.Id, (_wallHeight / 304.8 - roomLowerOffset), 0, false, false);
                            createdWall.get_Parameter(BuiltInParameter.WALL_BASE_OFFSET).Set(roomLowerOffset);
                            message.AppendLine($"Room ID: {roomId.Value}, Wall ID: {createdWall.Id.Value}");
                            createdWalls.Add(createdWall);

                            // Установка привязки стены к внутренней стороне
                            Parameter wallKeyRefParam = createdWall.get_Parameter(BuiltInParameter.WALL_KEY_REF_PARAM);
                            if (wallKeyRefParam != null && wallKeyRefParam.StorageType == StorageType.Integer)
                            {
                                wallKeyRefParam.Set(3); // 3 соответствует внутренней стороне стены
                            }

                            // Join walls 
                            if (boundaryElement != null && 
                                boundaryElement.Category.Id.Value == (int)BuiltInCategory.OST_Walls && 
                                createdWall != null)
                            {
                                JoinGeometryUtils.JoinGeometry(_doc, createdWall, boundaryElement);
                            }
                        }
                    }
                }
                trans.Commit();
                // Вызовите TrimExtendWalls после того как транзакция будет закрыта
                //TrimExtendWalls(_doc, createdWalls);
            }



        }

    }
}