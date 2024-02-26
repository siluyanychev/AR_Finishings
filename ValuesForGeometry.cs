using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Controls;

namespace AR_Finishings
{
    internal class ValuesForGeometry
    {
        private Document _doc;
        private const string RoomNumberParam = "DPM_AR_Отделка.Номер";
        private const string RoomNumbersParam = "DPM_AR_Отделка.Номера";

        public ValuesForGeometry(Document doc)
        {
            _doc = doc;
        }

        public void SetNumbersAndNamesToGeom()
        {
            // Получаем все типы элементов
            ElementTypeSelector selector = new ElementTypeSelector();
            var floorTypes = selector.GetFloorTypes(_doc);
            var ceilingTypes = selector.GetCeilingTypes(_doc);
            var wallTypes = selector.GetWallTypes(_doc);

            // Получаем ID категорий для каждого типа элемента
            var floorCategoryIds = floorTypes.Select(ft => ft.Category.Id);
            var ceilingCategoryIds = ceilingTypes.Select(ct => ct.Category.Id);
            var wallCategoryIds = wallTypes.Select(wt => wt.Category.Id);

            // Получаем все экземпляры элементов
            var floors = GetAllInstances(floorCategoryIds);
            var ceilings = GetAllInstances(ceilingCategoryIds);
            var walls = GetAllInstances(wallCategoryIds);

            // Сортируем и устанавливаем номера помещений
            SetRoomNumbersForType(floors, "Floors");
            SetRoomNumbersForType(ceilings, "Ceilings");
            SetRoomNumbersForType(walls, "Walls");
        }


        private void SetRoomNumbersForType(List<Element> elements, string category)
        {
            // Группировка элементов по типам
            var elementsByType = elements.GroupBy(e => e.GetTypeId()).ToList();

            using (Transaction trans = new Transaction(_doc, "Set Room Numbers For Type"))
            {
                trans.Start();

                // Обработка каждого типа
                foreach (var typeGroup in elementsByType)
                {
                    var typeElements = typeGroup.ToList();
                    var roomNumbersForType = new HashSet<string>();

                    // Сбор номеров помещений для текущего типа
                    foreach (var element in typeElements)
                    {
                        var roomNumber = element.LookupParameter(RoomNumberParam)?.AsString();
                        if (!string.IsNullOrEmpty(roomNumber))
                        {
                            roomNumbersForType.Add(roomNumber);
                        }
                    }

                    // Преобразование HashSet в строку
                    var combinedRoomNumbers = string.Join(", ", roomNumbersForType.OrderBy(n => n));

                    // Присвоение этой строки параметру DPM_AR_Отделка.Номера каждого экземпляра
                    foreach (var element in typeElements)
                    {
                        var roomNumbersParam = element.LookupParameter(RoomNumbersParam);
                        if (roomNumbersParam != null && roomNumbersParam.StorageType == StorageType.String)
                        {
                            roomNumbersParam.Set(combinedRoomNumbers);
                        }
                    }
                }

                trans.Commit();
            }
        }
        private List<Element> GetAllInstances(IEnumerable<ElementId> categoryIds)
        {
            List<Element> instances = new List<Element>();
            foreach (ElementId categoryId in categoryIds)
            {
                instances.AddRange(new FilteredElementCollector(_doc)
                                   .OfCategoryId(categoryId)
                                   .WhereElementIsNotElementType()
                                   .ToList());
            }
            return instances;
        }

    }
}

            