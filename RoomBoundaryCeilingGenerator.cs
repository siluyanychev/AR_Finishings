using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;
using Autodesk.Revit.UI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace AR_Finishings
{
    public class RoomBoundaryCeilingGenerator
    {
        private Document _doc;
        private double _ceilingHeight;

        public RoomBoundaryCeilingGenerator(Document doc, double ceilingHeight)
        {
            _doc = doc;
            _ceilingHeight = ceilingHeight; // Сохраняем значение высоты потолка из MainWindow
        }

        public void CreateCeilings(IEnumerable<ElementId> selectedRoomIds, CeilingType selectedCeilingType)
        {
            StringBuilder message = new StringBuilder("Generated Ceilings for Room IDs:\n");
            using (Transaction trans = new Transaction(_doc, "Generate Ceilings"))
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
                            SpatialElementBoundaryOptions options = new SpatialElementBoundaryOptions();
                            IList<IList<BoundarySegment>> boundaries = room.GetBoundarySegments(options);

                            if (boundaries.Count > 0)
                            {
                                // Основной контур потолка
                                CurveLoop mainCurveLoop = CurveLoop.Create(boundaries[0].Select(seg => seg.GetCurve()).ToList());
                                IList<CurveLoop> loops = new List<CurveLoop> { mainCurveLoop };

                                // Внутренние контуры (отверстия)
                                for (int i = 1; i < boundaries.Count; i++)
                                {
                                    CurveLoop innerCurveLoop = CurveLoop.Create(boundaries[i].Select(seg => seg.GetCurve()).ToList());
                                    loops.Add(innerCurveLoop); // Добавляем как отверстия в потолке
                                }

                                if (loops.Count > 0)
                                {
                                    Ceiling ceiling = Ceiling.Create(_doc, loops, selectedCeilingType.Id, level.Id);
                                    ceiling.get_Parameter(BuiltInParameter.CEILING_HEIGHTABOVELEVEL_PARAM).Set(UnitUtils.ConvertToInternalUnits(_ceilingHeight, UnitTypeId.Millimeters));
                                    SetupCeilingParameters( ceiling, roomNameValue, roomNumberValue, levelRoomStringValue );
                                }
                            }
                        }
                    }
                }
                trans.Commit();
            }
            
        }
        private void SetupCeilingParameters(Ceiling Ceiling, string roomNameValue, string roomNumberValue, string levelRoomStringValue)
        {

            // Пример установки значения общего параметра (предполагая, что параметр уже добавлен в проект)
            Guid roomNameGuid = new Guid("4a5cec5d-f883-42c3-a05c-89ec822d637b"); // GUID общего параметра
            Parameter roomNameParam = Ceiling.get_Parameter(roomNameGuid);
            if (roomNameParam != null && roomNameParam.StorageType == StorageType.String)
            {
                roomNameParam.Set(roomNameValue); // Установка значения параметра
            }
            // Пример установки значения общего параметра (предполагая, что параметр уже добавлен в проект)
            Guid roomNumberGuid = new Guid("317bbea6-a1a8-4923-a722-635c998c184d"); // GUID общего параметра
            Parameter roomNumberParam = Ceiling.get_Parameter(roomNumberGuid);
            if (roomNumberParam != null && roomNumberParam.StorageType == StorageType.String)
            {
                roomNumberParam.Set(roomNumberValue); // Установка значения параметра
            }
            // Пример установки значения общего параметра (предполагая, что параметр уже добавлен в проект)
            Guid levelGuid = new Guid("9eabf56c-a6cd-4b5c-a9d0-e9223e19ea3f"); // GUID общего параметра
            Parameter CeilingLevelParam = Ceiling.get_Parameter(levelGuid);
            if (CeilingLevelParam != null && CeilingLevelParam.StorageType == StorageType.String)
            {
                CeilingLevelParam.Set(levelRoomStringValue); // Установка значения параметра
            }

        }

    }
}
