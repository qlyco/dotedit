using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace DotEdit
{
    /// <summary>
    /// Interaction logic for ImageWizard.xaml
    /// </summary>
    public partial class ImageWizard : Window
    {
        public int ImageWidth { get; set; } = 0;
        public int ImageHeight { get; set; } = 0;

        public ImageWizard()
        {
            InitializeComponent();
        }

        private void CancelBtn_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }

        private void CreateBtn_Click(object sender, RoutedEventArgs e)
        {
            ImageWidth = (int)Math.Pow(2, WidthInput.SelectedIndex + 3);
            ImageHeight = (int)Math.Pow(2, HeightInput.SelectedIndex + 3);

            DialogResult = true;
        }
    }
}
