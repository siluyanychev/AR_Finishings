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
            // Группировка элементов по типам с уже заполненным параметром DPM_AR_Отделка.Номер
            var elementsByType = elements.Where(e => e.LookupParameter(RoomNumberParam)?.AsString() != null)
                                         .GroupBy(e => e.GetTypeId()).ToList();

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

        // И использование этого метода для кажд

        private List<Element> GetAllInstances(IEnumerable<ElementId> typeIds)
        {
            List<Element> instancesWithRoomNumber = new List<Element>();
            foreach (ElementId typeId in typeIds)
            {
                var elements = new FilteredElementCollector(_doc)
                               .OfClass(typeof(Floor)) // Замените на соответствующий класс элемента
                               .OfCategory(BuiltInCategory.OST_Floors)
                               .WhereElementIsNotElementType()
                               .ToList();

                // Фильтруем элементы, которые имеют значение в параметре DPM_AR_Отделка.Номер
                var filteredElements = elements.Where(e =>
                {
                    var param = e.LookupParameter(RoomNumberParam);
                    return param != null && param.HasValue && !string.IsNullOrWhiteSpace(param.AsString());
                });

                instancesWithRoomNumber.AddRange(filteredElements);
            }
            return instancesWithRoomNumber;
        }

    }
}

            