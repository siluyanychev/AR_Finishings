using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AR_Finishings
{
    public class Preparations
    {
        public StringBuilder ResultMessage { get; } = new StringBuilder();

        private bool CheckPathToSharedParameter()
        {
            Task<bool> vpnTask = Task.Run(() =>
            {
                PathChecker vpnChecker = new PathChecker();
                return vpnChecker.IsPathAccess();
            });

            return vpnTask.Wait(TimeSpan.FromSeconds(2));
        }

        public void GetParameters(Document doc)
        {
            string[] parameterGeometryName =
            {
                "DPM_AR_Отделка.Имя",
                "DPM_AR_Отделка.Имена",
                "DPM_AR_Отделка.Номер",
                "DPM_AR_Отделка.Номера"
            };
            string[] parameterRooms =
            {
                "DPM_AR_Отделка.Плинтусы",
                "DPM_AR_Отделка.Плинтусы.Длина",
                "DPM_AR_Отделка.Колонны",
                "DPM_AR_Отделка.Колонны.Площадь",
                "DPM_AR_Отделка.Полы",
                "DPM_AR_Отделка.Полы.Площадь",
                "DPM_AR_Отделка.Потолки",
                "DPM_AR_Отделка.Потолки.Площадь",
                "DPM_AR_Отделка.Потолки.Высота",
                "DPM_AR_Отделка.Стены",
                "DPM_AR_Отделка.Стены.Площадь"
            };

            if (!CheckPathToSharedParameter())
            {
                TaskDialog.Show("Подключите VPN", "Для загрузки параметра необходимо подключить VPN.");
                return;
            }

            BuiltInCategory[] categories =
            {
                BuiltInCategory.OST_Ceilings,
                BuiltInCategory.OST_Floors,
                BuiltInCategory.OST_Walls
            };

            BuiltInCategory rooms = BuiltInCategory.OST_Rooms;

            bool parameterApplied = false;

            foreach (var category in categories)
            {
                bool isParameterApplied = CheckIfParametersApplied(doc, category, parameterGeometryName);
                if (!isParameterApplied)
                {
                    ApplyParameters(doc, new BuiltInCategory[] { category }, parameterGeometryName, "02 Обязательные АРХИТЕКТУРА");
                    parameterApplied = true;
                }
            }

            string roomsGroupName = "02 Обязательные АРХИТЕКТУРА";

            bool isRoomParameterApplied = CheckIfParametersApplied(doc, rooms, parameterRooms);
            if (!isRoomParameterApplied)
            {
                ApplyParameters(doc, new BuiltInCategory[] { rooms }, parameterRooms, roomsGroupName);
                parameterApplied = true;
            }

            // Применяем общий параметр ADSK_Этаж к категориям из categories и категории rooms
            string[] commonParameter = { "ADSK_Этаж" };
            foreach (var category in categories.Concat(new[] { rooms }))
            {
                bool isCommonParameterApplied = CheckIfParametersApplied(doc, category, commonParameter);
                if (!isCommonParameterApplied)
                {
                    ApplyParameters(doc, categories.Concat(new[] { rooms }).ToArray(), new[] { "ADSK_Этаж" }, "01 Обязательные ОБЩИЕ");
                    parameterApplied = true;
                }
            }

            if (!parameterApplied)
            {
                TaskDialog.Show("Результат", "Параметры уже применены к всем необходимым категориям.");
            }
            else
            {
                TaskDialog.Show("Результат", "Параметры успешно применены.");
            }
        }

        private bool CheckIfParametersApplied(Document doc, BuiltInCategory category, string[] parameterNames)
        {
            Category cat = doc.Settings.Categories.get_Item(category);
            CategorySet categories = new CategorySet();
            categories.Insert(cat);

            BindingMap bindingMap = doc.ParameterBindings;
            DefinitionBindingMapIterator it = bindingMap.ForwardIterator();
            it.Reset();

            while (it.MoveNext())
            {
                Definition definition = it.Key;
                ElementBinding binding = (ElementBinding)it.Current;

                foreach (string paramName in parameterNames)
                {
                    if (definition.Name == paramName && binding.Categories.Contains(cat))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private void ApplyParameters(Document doc, BuiltInCategory[] categories, string[] parameterNames, string groupName)
        {
            using (Transaction t = new Transaction(doc, "Применение параметров к элементам"))
            {
                t.Start();

                DefinitionFile sharedParameterFile = doc.Application.OpenSharedParameterFile();
                if (sharedParameterFile == null)
                {
                    throw new InvalidOperationException("Файл общих параметров не найден.");
                }

                DefinitionGroup group = sharedParameterFile.Groups.get_Item(groupName);
                if (group == null)
                {
                    throw new InvalidOperationException("Группа параметров не найдена.");
                }

                BindingMap bindingMap = doc.ParameterBindings;
                bool anyBindingsAdded = false;

                foreach (var catEnum in categories)
                {
                    Category cat = doc.Settings.Categories.get_Item(catEnum);
                    CategorySet categorySet = new CategorySet();
                    categorySet.Insert(cat);

                    InstanceBinding instanceBinding = doc.Application.Create.NewInstanceBinding(categorySet);

                    foreach (var parameterName in parameterNames)
                    {
                        ExternalDefinition definition = group.Definitions.get_Item(parameterName) as ExternalDefinition;
                        if (definition == null)
                        {
                            // Если параметр не найден, пропускаем его
                            continue;
                        }

                        bool bindSuccess = bindingMap.Insert(definition, instanceBinding, BuiltInParameterGroup.PG_DATA);
                        if (!bindSuccess && bindingMap.Contains(definition))
                        {
                            bindingMap.ReInsert(definition, instanceBinding, BuiltInParameterGroup.PG_DATA);
                        }

                        if (bindSuccess)
                        {
                            anyBindingsAdded = true;
                        }
                    }
                }

                if (anyBindingsAdded)
                {
                    t.Commit();
                }
                else
                {
                    t.RollBack();
                }
            }
        }


    }
}