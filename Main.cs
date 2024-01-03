using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using System;
using System.Globalization;
using System.Reflection;
using System.Resources;
using System.Windows.Interop;

namespace AR_Finishings
{

    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class Main : IExternalCommand

    {
        private ResourceManager rm = new ResourceManager("AR_Finishings.Strings", Assembly.GetExecutingAssembly());
        private CultureInfo ci = CultureInfo.CurrentCulture;

        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            try
            {
                UIDocument uidoc = commandData.Application.ActiveUIDocument;
                Document doc = uidoc.Document;
                MainWindow mainWindow = new MainWindow(commandData);


                // Отобразите модальное окно и дождитесь его закрытия
                IntPtr mainWindowHandle = System.Diagnostics.Process.GetCurrentProcess().MainWindowHandle;
                WindowInteropHelper helper = new WindowInteropHelper(mainWindow);
                helper.Owner = mainWindowHandle;

                mainWindow.ShowDialog();


                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                TaskDialog.Show(rm.GetString("Error"), rm.GetString("AnErrorOccurred") + ex.Message);
                return Result.Failed;
            }
        }
    }
}