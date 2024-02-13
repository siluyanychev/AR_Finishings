using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using System;
using System.Text;
using System.Threading.Tasks;

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
            string parameterName = "DPM_X_Слои.Состав";

            // Проверка соединения с VPN перед началом работы с категориями
            if (!CheckPathToSharedParameter())
            {
                // Время ожидания истекло, показываем сообщение об ошибке
                TaskDialog.Show("Подключите VPN", "Для загрузки параметра необходимо подключить VPN.");
                return; // Выход из метода, если нет VPN
            }

            // Определение интересующих категорий
            BuiltInCategory[] categories = new BuiltInCategory[]
            {
            BuiltInCategory.OST_Ceilings,
            BuiltInCategory.OST_Floors,
            BuiltInCategory.OST_Roofs,
            BuiltInCategory.OST_Walls
            };

            // Переменная для отслеживания, был ли применен параметр хотя бы к одной категории
            bool parameterApplied = false;

            foreach (var category in categories)
            {
                // Проверка, применен ли параметр
                bool isParameterApplied = CheckIfParameterApplied(doc, category, parameterName);

                if (!isParameterApplied)
                {
                    // Применение параметра
                    ApplyParameter(doc, categories, parameterName);
                    parameterApplied = true; // Отмечаем, что параметр был применен
                }
            }

            // Если параметр не был применен ни к одной категории, сообщаем об этом
            if (!parameterApplied)
            {
                TaskDialog.Show("Результат", "Параметры не были применены к какой-либо категории.");
            }
        }


        private bool CheckIfParameterApplied(Document doc, BuiltInCategory category, string parameterName)
        {
            // Получаем список всех параметров для данной категории
            Category cat = doc.Settings.Categories.get_Item(category);
            CategorySet categories = new CategorySet();
            categories.Insert(cat);

            // Проверяем наличие параметра в BindingMap
            BindingMap bindingMap = doc.ParameterBindings;
            DefinitionBindingMapIterator it = bindingMap.ForwardIterator();
            it.Reset();

            while (it.MoveNext())
            {
                Definition definition = it.Key;
                // Проверяем, соответствует ли имя параметра заданному имени и применен ли он к нужной категории
                if (definition.Name == parameterName)
                {
                    ElementBinding binding = (ElementBinding)it.Current;
                    if (binding.Categories.Contains(cat))
                    {
                        return true; // Параметр применен
                    }
                }
            }

            return false; // Параметр не найден
        }


        private void ApplyParameter(Document doc, BuiltInCategory[] categories, string parameterName)
        {
            using (Transaction t = new Transaction(doc, "Применен DPM_X_Слои.Состав к элементам типа"))
            {
                t.Start();

                DefinitionFile sharedParameterFile = doc.Application.OpenSharedParameterFile();
                if (sharedParameterFile == null)
                {
                    throw new InvalidOperationException("Файл общих параметров не найден, или влючите VPN на диск 'К' или добавьте его по пути K:\\BIM\\X\\04_Данные\\01_Общие параметры_TXT  ");
                }

                DefinitionGroup group = sharedParameterFile.Groups.get_Item("02 Обязательные АРХИТЕКТУРА");
                if (group == null)
                {
                    throw new InvalidOperationException("Parameter group not found.");
                }

                ExternalDefinition definition = group.Definitions.get_Item(parameterName) as ExternalDefinition;
                if (definition == null)
                {
                    throw new InvalidOperationException("Parameter definition not found.");
                }

                // Create a CategorySet for all the categories
                CategorySet categorySet = new CategorySet();
                foreach (BuiltInCategory catEnum in categories)
                {
                    Category cat = doc.Settings.Categories.get_Item(catEnum);
                    categorySet.Insert(cat);
                }

                // Create a TypeBinding for the entire category set
                TypeBinding typeBinding = doc.Application.Create.NewTypeBinding(categorySet);

                BindingMap bindingMap = doc.ParameterBindings;
                bool bindSuccess = bindingMap.Insert(definition, typeBinding, GroupTypeId.Data);

                if (!bindSuccess)
                {
                    // If the parameter already exists in the project, rebind with the new definition
                    if (bindingMap.Contains(definition))
                    {
                        bindSuccess = bindingMap.ReInsert(definition, typeBinding, GroupTypeId.Data);
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