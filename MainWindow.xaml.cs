using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Resources;
using System.Text;
using System.Windows;

namespace AR_Finishings
{

    public partial class MainWindow
    {
        private ResourceManager rm = new ResourceManager("AR_Finishings.Strings", Assembly.GetExecutingAssembly());
        private CultureInfo ci = CultureInfo.CurrentCulture;
        private StringBuilder resultMessage = new StringBuilder();
        public ExternalCommandData commandData;
        private Document mainDocument;


        public MainWindow(ExternalCommandData commandData)
        {
            InitializeComponent();
            this.DataContext = this;
            this.commandData = commandData;
            this.mainDocument = commandData.Application.ActiveUIDocument.Document;
        }
        public string WallTypes => rm.GetString("WallTypes", ci);

        private void CheckBox_Checked(object sender, RoutedEventArgs e)
        {

        }

        private void CheckBox_Checked_1(object sender, RoutedEventArgs e)
        {

        }
    }
}
