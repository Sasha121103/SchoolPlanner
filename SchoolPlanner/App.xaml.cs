using System.Windows;

using SchoolPlanner.Database;

namespace SchoolPlanner
{
    public partial class App : Application
    {
        public static User CurrentUser { get; set; }

        // Используем new чтобы скрыть наследуемый член
        public new static Window MainWindow { get; set; }
    }
}