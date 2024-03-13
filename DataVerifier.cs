using Autodesk.Revit.UI;
using Autodesk.Revit.DB;
using System;
using System.IO;
using System.Text.RegularExpressions;
using System.Linq;

namespace AR_Finishings
{
    public class DataVerifier
    {
        public static bool VerifyData(ExternalCommandData commandData)
        {
            // Получаем имя пользователя из Revit
            string revitUserName = commandData.Application.Application.Username;

            // Получаем имя активного файла проекта
            Document doc = commandData.Application.ActiveUIDocument.Document;
            string fileName = doc.PathName;

            // Проверяем, состоит ли имя пользователя Revit из 3 заглавных букв
            bool isRevitUserNameValid = Regex.IsMatch(revitUserName, @"^[A-Z]{3}$");

            // Проверяем, начинается ли имя файла с "AR_"
            bool isFileNameValid = System.IO.Path.GetFileName(fileName).StartsWith("AR_");

            // Получаем имя пользователя Windows
            string windowsUserName = Environment.UserName;

            // Получаем версию Revit из командных данных
            string revitVersion = commandData.Application.Application.VersionName;

            // Строим путь к файлу Revit.ini
            string revitIniPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                                               "Autodesk", "Revit", $"{revitVersion}", "Revit.ini");
            // Проверяем существование файла Revit.ini
            if (!File.Exists(revitIniPath))

                // Проверяем существование файла Revit.ini
                if (!File.Exists(revitIniPath))
                {
                    TaskDialog.Show("Ошибка", "Файл настроек Revit.ini не найден.");
                    return false;
                }

            // Проверяем содержание файла на наличие нужной строки
            bool isExternalParametersValid = false;
            foreach (string line in File.ReadLines(revitIniPath))
            {
                if (line.StartsWith("ExternalParameters=") && line.EndsWith("ДПМ_Файл общих параметров.txt"))
                {
                    isExternalParametersValid = true;
                    break;
                }
            }


            // Строим путь к файлу logs.txt в Teams
            string teamsLogPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                                               "Microsoft", "Teams", "logs.txt");

            // Проверяем существование файла logs.txt
            if (!File.Exists(teamsLogPath))
            {
                TaskDialog.Show("Ошибка", "Файл логов Teams не найден.");
                return false;
            }

            // Проверяем содержание файла logs.txt на наличие нужной строки
            bool isCorporateParametersValid = false;
            foreach (string line in File.ReadLines(teamsLogPath))
            {
                if (line.Contains("@dpm.global"))
                {
                    isCorporateParametersValid = true;
                    break;
                }
            }

            // Проверяем все условия вместе
            if (!isRevitUserNameValid || !isFileNameValid || !isExternalParametersValid || !isCorporateParametersValid)
            {
                TaskDialog.Show("Ошибка", "Верификация конфигурации не пройдена. Обратитесь к разработчику.");
                return false;
            }

            // Если все проверки пройдены, возвращаем true
            return true;
        }
    }
}
