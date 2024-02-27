using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using System.Collections.Generic;
using System.Linq;

namespace AR_Finishings
{
    internal class ValuesForCeilings
    {
        private Document _doc;
        private const string RoomNumberParam = "DPM_AR_Отделка.Номер";
        private const string RoomNumbersParam = "DPM_AR_Отделка.Номера";
        private const string RoomNameParam = "DPM_AR_Отделка.Имя";
        private const string RoomNamesParam = "DPM_AR_Отделка.Имена";

        public ValuesForCeilings(Document doc)
        {
            _doc = doc;
        }

        public void SetToGeom()
        {
            // Получаем все типы полов
            ElementTypeSelector selector = new ElementTypeSelector();
            var ceilingTypes = selector.GetCeilingTypes(_doc);

            // Получаем ID категорий для типов полов
            var ceilingCategoryIds = ceilingTypes.Select(ft => ft.Category.Id);

            // Получаем все экземпляры полов
            var ceilings = GetAllInstances(ceilingCategoryIds);

            // Сортируем и устанавливаем номера помещений для полов
            SetRoomNumbersAndNamesForType(ceilings, "Ceilings");
        }

        private void SetRoomNumbersAndNamesForType(List<Element> elements, string category)
        {
            // Группировка элементов по типам с уже заполненным параметром RoomNumberParam
            var elementsByType = elements.Where(e => e.LookupParameter(RoomNumberParam)?.AsString() != null)
                                         .GroupBy(e => e.GetTypeId()).ToList();

            using (Transaction trans = new Transaction(_doc, "Set Room Numbers and Names for " + category))
            {
                trans.Start();

                // Обработка каждого типа
                foreach (var typeGroup in elementsByType)
                {
                    var roomNumbersForType = new HashSet<string>();
                    var roomNamesForType = new List<string>();  // Для хранения имен помещений

                    // Сбор номеров и имен помещений для текущего типа
                    foreach (var element in typeGroup)
                    {
                        var roomNumber = element.LookupParameter(RoomNumberParam)?.AsString();
                        var roomName = element.LookupParameter(RoomNameParam)?.AsString();
                        if (!string.IsNullOrEmpty(roomNumber))
                        {
                            roomNumbersForType.Add(roomNumber);
                        }
                        if (!string.IsNullOrEmpty(roomName))
                        {
                            // Если имя уже есть в списке, не добавляем его снова
                            if (!roomNamesForType.Contains(roomName))
                            {
                                roomNamesForType.Add(roomName);
                            }
                        }
                    }

                    // Преобразование HashSet и List в строки
                    var combinedRoomNumbers = string.Join(", ", roomNumbersForType.OrderBy(n => n));
                    var combinedRoomNames = string.Join(", ", roomNamesForType.OrderBy(n => n));  // Имена могут повторяться, поэтому не используем HashSet

                    // Присвоение этих строк параметрам каждого экземпляра
                    foreach (var element in typeGroup)
                    {
                        var roomNumbersParam = element.LookupParameter(RoomNumbersParam);
                        var roomNamesParam = element.LookupParameter(RoomNamesParam);
                        if (roomNumbersParam != null && roomNumbersParam.StorageType == StorageType.String)
                        {
                            roomNumbersParam.Set(combinedRoomNumbers);
                        }
                        if (roomNamesParam != null && roomNamesParam.StorageType == StorageType.String)
                        {
                            roomNamesParam.Set(combinedRoomNames);
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
                               .OfClass(typeof(Ceiling)) // Замените на соответствующий класс элемента
                               .OfCategory(BuiltInCategory.OST_Ceilings)
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

