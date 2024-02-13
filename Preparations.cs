using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using System;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;

namespace AR_Finishings
{
    public class Preparations
    {
        public StringBuilder ResultMessage { get; } = new StringBuilder();
        // Метод для проверки доступности VPN
        private bool CheckPathToSharedParameter()
        {
            Task<bool> vpnTask = Task.Run(() =>
            {
                PathChecker vpnChecker = new PathChecker();
                return vpnChecker.IsPathAccess();
            });

            // Ожидание результатов проверки VPN в течение заданного времени
            return vpnTask.Wait(TimeSpan.FromSeconds(2));
        }

        // Основной метод для применения параметров
        public void GetParameters(Document doc)
        {
            // Определение имени параметра
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

            bool parameterApplied = false; // Переменная для отслеживания статуса применения параметров

            foreach (var category in categories)
            {
                bool isParameterApplied = CheckIfParametersApplied(doc, category, parameterGeometryName);
                if (!isParameterApplied)
                {
                    ApplyParameters(doc, new BuiltInCategory[] { category }, parameterGeometryName);
                    parameterApplied = true;
                }
            }
            bool isRoomParameterApplied = CheckIfParametersApplied(doc, rooms, parameterRooms);
            if (!isRoomParameterApplied)
            {
                ApplyParameters(doc, new BuiltInCategory[] { rooms }, parameterRooms);
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
                        return true; // Параметр применен
                    }
                }
            }

            return false; // Ни один из параметров не найден
        }

        private void ApplyParameters(Document doc, BuiltInCategory[] categories, string[] parameterNames)
        {
            using (Transaction t = new Transaction(doc, "Применены параметры к элементам"))
            {
                t.Start();

                DefinitionFile sharedParameterFile = doc.Application.OpenSharedParameterFile();
                if (sharedParameterFile == null)
                {
                    throw new InvalidOperationException("Файл общих параметров не найден.");
                }

                DefinitionGroup group = sharedParameterFile.Groups.get_Item("02 Обязательные АРХИТЕКТУРА");
                if (group == null)
                {
                    throw new InvalidOperationException("Parameter group not found.");
                }

                CategorySet categorySet = new CategorySet();
                foreach (BuiltInCategory catEnum in categories)
                {
                    Category cat = doc.Settings.Categories.get_Item(catEnum);
                    categorySet.Insert(cat);
                }

                InstanceBinding InstanceBinding = doc.Application.Create.NewInstanceBinding(categorySet);
                BindingMap bindingMap = doc.ParameterBindings;

                foreach (var parameterName in parameterNames)
                {
                    ExternalDefinition definition = group.Definitions.get_Item(parameterName) as ExternalDefinition;
                    if (definition == null)
                    {
                        continue; // Если параметр не найден, пропускаем его
                    }

                    bool bindSuccess = bindingMap.Insert(definition, InstanceBinding, GroupTypeId.IdentityData);
                    if (!bindSuccess && bindingMap.Contains(definition))
                    {
                        bindSuccess = bindingMap.ReInsert(definition, InstanceBinding, GroupTypeId.IdentityData);
                    }
                    if (!bindSuccess)
                    {
                        throw new InvalidOperationException("Failed to bind parameter.");
                    }
                }

                t.Commit();
            }
        }
    }
}