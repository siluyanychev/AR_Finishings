using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;
using Autodesk.Revit.UI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Media;
using System.Xaml;

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
                    string roomNameValue = room.get_Parameter(BuiltInParameter.ROOM_NAME).AsString();
                    string roomNumberValue = room.get_Parameter(BuiltInParameter.ROOM_NUMBER).AsString();
                    string levelRoomStringValue = room.get_Parameter(BuiltInParameter.LEVEL_NAME).AsString().Split('_')[1];
                    Level level = _doc.GetElement(room.LevelId) as Level;
                    double roomLowerOffset = room.get_Parameter(BuiltInParameter.ROOM_LOWER_OFFSET).AsDouble();

                    var boundaries = room.GetBoundarySegments(new SpatialElementBoundaryOptions());
                    foreach (var boundary in boundaries)
                    {
                        foreach (var segment in boundary)
                        {
                            Element boundaryElement = _doc.GetElement(segment.ElementId);

                            // Проверяем, является ли элемент стеной с CurtainGrid или имя типа начинается с "АР_О"
                            Wall boundaryWall = boundaryElement as Wall;

                            if (boundaryWall != null)
                            {
                                WallType wallType = _doc.GetElement(boundaryWall.GetTypeId()) as WallType;
                                if (boundaryWall.CurtainGrid != null || (wallType != null && wallType.Name.StartsWith("АР_О")))
                                {
                                    // Пропускаем создание стены, если это витраж или тип стены начинается с "АР_О"
                                    continue;
                                }
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

                            // Join walls 
                            if (boundaryElement != null &&
                                boundaryElement.Category.Id.Value == (int)BuiltInCategory.OST_Walls &&
                                createdWall != null)
                            {
                                JoinGeometryUtils.JoinGeometry(_doc, createdWall, boundaryElement);
                            }
                            SetupWallParameters(createdWall, roomLowerOffset, roomNameValue, roomNumberValue, levelRoomStringValue);
                        }
                    }
                }
                trans.Commit();
                // Вызовите TrimExtendWalls после того как транзакция будет закрыта
                //TrimExtendWalls(_doc, createdWalls);
            }



        }
        private void SetupWallParameters(Wall wall, double roomLowerOffset, string roomNameValue, string roomNumberValue, string levelRoomStringValue)
        {
            wall.get_Parameter(BuiltInParameter.WALL_BASE_OFFSET).Set(roomLowerOffset);

            Parameter wallKeyRefParam = wall.get_Parameter(BuiltInParameter.WALL_KEY_REF_PARAM);
            if (wallKeyRefParam != null && wallKeyRefParam.StorageType == StorageType.Integer)
            {
                wallKeyRefParam.Set(3); // Установка внутренней стороны стены
            }
            // Пример установки значения общего параметра (предполагая, что параметр уже добавлен в проект)
            Guid roomNameGuid = new Guid("4a5cec5d-f883-42c3-a05c-89ec822d637b"); // GUID общего параметра
            Parameter roomNameParam = wall.get_Parameter(roomNameGuid);
            if (roomNameParam != null && roomNameParam.StorageType == StorageType.String)
            {
                roomNameParam.Set(roomNameValue); // Установка значения параметра
            }
            // Пример установки значения общего параметра (предполагая, что параметр уже добавлен в проект)
            Guid roomNumberGuid = new Guid("317bbea6-a1a8-4923-a722-635c998c184d"); // GUID общего параметра
            Parameter roomNumberParam = wall.get_Parameter(roomNumberGuid);
            if (roomNumberParam != null && roomNumberParam.StorageType == StorageType.String)
            {
                roomNumberParam.Set(roomNumberValue); // Установка значения параметра
            }
            // Пример установки значения общего параметра (предполагая, что параметр уже добавлен в проект)
            Guid levelGuid = new Guid("9eabf56c-a6cd-4b5c-a9d0-e9223e19ea3f"); // GUID общего параметра
            Parameter wallLevelParam = wall.get_Parameter(levelGuid);
            if (wallLevelParam != null && wallLevelParam.StorageType == StorageType.String)
            {
                wallLevelParam.Set(levelRoomStringValue); // Установка значения параметра
            }

        }

    }
}