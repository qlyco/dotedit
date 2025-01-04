using Microsoft.VisualBasic;
using Microsoft.Win32;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Media;
using System.Text;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Media.Media3D;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace DotEdit
{
    public partial class Editor : Window
    {
        private int scale = 1;

        private const int CANVAS_BASE_WIDTH = 512;
        private const int CANVAS_BASE_HEIGHT = 512;
        private const int PREVIEW_BASE_WIDTH = 128;
        private const int MIN_ZOOM_LEVEL = 1;
        private const int MAX_ZOOM_LEVEL = 5;
        private const int GRID_SIZE = 8;

        private enum ColourSelection
        {
            NONE = -1,
            PRIMARY = 0,
            SECONDARY = 1
        }

        private int canvasWidthAspect = 1;
        private int canvasHeightAspect = 1;

        private ImageData? image;
        private WriteableBitmap bmp;

        private int[] selectedColour = { 0, 1 };
        private int zoomLevel = 1;
        private ColourSelection clicking = ColourSelection.NONE;

        private int startX = 0;
        private int startY = 0;

        private Stack<IAction> undoStack = [];
        private Stack<IAction> redoStack = [];

        private Button currentTool = new();

        private Tool selectedTool = Tool.PENCIL;

        private readonly Button[] swatches = new Button[(int)Math.Pow(2, ImageData.BIT_PER_PIXEL)];

        private string fileURL = "";

        public enum Tool
        {
            PENCIL,
            FILL,
            LINE,
            RECT,
            ELLIPSES,
        }

        public Editor()
        {
            InitializeComponent();
            
            RenderOptions.SetBitmapScalingMode(CanvasSurface, BitmapScalingMode.NearestNeighbor);
            RenderOptions.SetBitmapScalingMode(Preview, BitmapScalingMode.NearestNeighbor);

            ChangeControlState(false);

            swatches =
            [
                Swatch1, Swatch2, Swatch3, Swatch4, Swatch5, Swatch6, Swatch7, Swatch8, Swatch9, Swatch10, Swatch11, Swatch12, Swatch13, Swatch14, Swatch15, Swatch16
            ];

            bmp = new(1, 1, 300, 300, PixelFormats.Bgr32, null);
        }

        private void Swatch_Click(object sender, RoutedEventArgs e)
        {
            if (image == null)
            {
                return;
            }

            if (((Button) sender).Tag != null)
            {
                int index = int.Parse(((Button)sender).Tag.ToString()!);
                selectedColour[(int)ColourSelection.PRIMARY] = index;
            }

            UpdateSelectedColour(ColourSelection.PRIMARY);
        }

        private void Swatch_Click(object sender, MouseButtonEventArgs e)
        {
            if (image == null)
            {
                return;
            }

            if (((Button)sender).Tag != null)
            {
                int index = int.Parse(((Button)sender).Tag.ToString()!);
                selectedColour[(int)ColourSelection.SECONDARY] = index;
            }

            UpdateSelectedColour(ColourSelection.SECONDARY);
        }

        private void UpdateSelectedColour(ColourSelection id)
        {
            if (image == null)
            {
                return;
            }

            byte a = image.Palette[selectedColour[(int)id]][(int)ImageData.ColourChannelIndex.ALPHA];
            byte r = image.Palette[selectedColour[(int)id]][(int)ImageData.ColourChannelIndex.RED];
            byte g = image.Palette[selectedColour[(int)id]][(int)ImageData.ColourChannelIndex.GREEN];
            byte b = image.Palette[selectedColour[(int)id]][(int)ImageData.ColourChannelIndex.BLUE];

            SolidColorBrush brush = new SolidColorBrush(System.Windows.Media.Color.FromArgb(a, r, g, b));

            switch (id)
            {
                case ColourSelection.PRIMARY:
                    UpdateSliders(r, g, b);
                    SelectedColour.Background = brush;
                    break;
                case ColourSelection.SECONDARY:
                    SelectedAltColour.Background = brush;
                    break;
            }
        }

        private void UpdateSliders(byte r, byte g, byte b)
        {
            RedSlider.Value = r;
            GreenSlider.Value = g;
            BlueSlider.Value = b;
        }

        private void Swatch_DoubleClick(object sender, MouseButtonEventArgs e)
        {
            // CURRENTLY UNUSED
        }

        private void CreateNewImage(int width, int height, byte[][]? palette = null)
        {
            if (width < ImageData.MIN_CANVAS_SIZE || width > ImageData.MAX_CANVAS_SIZE || height < ImageData.MIN_CANVAS_SIZE || height > ImageData.MAX_CANVAS_SIZE)
            {
                image = null;
            } else
            {
                try
                {
                    image = new ImageData(width, height, palette);
                } catch (Exception ex)
                {
                    image = null;
                    MessageBox.Show(this, ex.Message, "Error creating file", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void CalculateCanvasAspect(int width, int height)
        {
            canvasWidthAspect = Math.Max(height / width, 1);
            canvasHeightAspect = Math.Max(width / height, 1);
        }

        private void NewFile_Click(object sender, RoutedEventArgs e)
        {
            if (!SavePrompt())
            {
                return;
            }

            ImageWizard dialog = new()
            {
                Owner = this
            };

            if (dialog.ShowDialog() != true)
            {
                return;
            }

            CreateNewImage(dialog.ImageWidth, dialog.ImageHeight);

            if (image != null)
            {
                SetupEditor();
            }
        }

        private void SetupEditor(string url = "")
        {
            if (image != null)
            {
                fileURL = url;
                CalculateCanvasAspect(image.Width, image.Height);
                bmp = new(image.Width, image.Height, 300, 300, PixelFormats.Bgr32, null);

                CanvasSurface.Source = bmp;
                Preview.Source = bmp;

                selectedColour = [0, 1];
                zoomLevel = MIN_ZOOM_LEVEL;

                Canvas.Width = CANVAS_BASE_WIDTH / canvasWidthAspect;
                Canvas.Height = CANVAS_BASE_HEIGHT / canvasHeightAspect;

                CanvasSurface.Width = CANVAS_BASE_WIDTH / canvasWidthAspect;
                CanvasSurface.Height = CANVAS_BASE_HEIGHT / canvasHeightAspect;

                GridDisplay.Width = Canvas.Width;
                GridDisplay.Height = Canvas.Height;

                SetupGrid(image.Width, image.Height, zoomLevel, GRID_SIZE);

                scale = (int)Canvas.Width / image.Width;

                Preview.RenderSize = CanvasSurface.RenderSize;
                Preview.Width = PREVIEW_BASE_WIDTH / canvasWidthAspect;
                Preview.Height = PREVIEW_BASE_WIDTH / canvasHeightAspect;

                currentTool.Tag = "";
                currentTool = PencilBtn;
                currentTool.Tag = "Active";

                selectedTool = Tool.PENCIL;

                undoStack = [];
                redoStack = [];

                ChangeControlState(true);
                UpdateState();
            }
            else
            {
                ChangeControlState(false);
            }
        }

        private void SetupGrid(int width, int height, int zoom, int size)
        {
            GridDisplay.ColumnDefinitions.Clear();
            GridDisplay.RowDefinitions.Clear();

            int aspectW = Math.Max(width / height, 1);
            int aspectH = Math.Max(height / width, 1);

            int subdivideW = aspectW;
            int subdivideH = aspectH;

            int gridCountX = width / size;
            int gridCountY = height / size;

            if (zoom > 3 || Math.Max(width, height) <= 16)
            {
                subdivideW = width;
                subdivideH = height;
            } else if (zoom == 3)
            {
                subdivideW = gridCountX;
                subdivideH = gridCountY;
            } else
            {
                subdivideW *= zoom * 2;
                subdivideH *= zoom * 2;
            }

            for (int i = 0; i < subdivideW; i++)
            {
                GridDisplay.ColumnDefinitions.Add(new());
            }

            for (int i = 0; i < subdivideH; i++)
            {
                GridDisplay.RowDefinitions.Add(new());
            }
        }

        private void ChangeControlState(bool enable)
        {
            if (enable)
            {
                SidebarControls.Visibility = Visibility.Visible;
                SaveBtn.IsEnabled = true;
                SaveAsBtn.IsEnabled = true;
                LoadPaletteBtn.IsEnabled = true;
                SavePaletteBtn.IsEnabled = true;
                CloseBtn.IsEnabled = true;
                Canvas.Visibility = Visibility.Visible;
                ZoomLvText.Content = $"X{zoomLevel}";

                UpdatePaletteUI();
            } else
            {
                SidebarControls.Visibility = Visibility.Collapsed;
                SaveBtn.IsEnabled = false;
                SaveAsBtn.IsEnabled = false;
                LoadPaletteBtn.IsEnabled = false;
                SavePaletteBtn.IsEnabled = false;
                CloseBtn.IsEnabled = false;
                Canvas.Visibility = Visibility.Collapsed;
            }
        }

        private void Canvas_MouseMove(object sender, MouseEventArgs e)
        {
            if (image == null)
            {
                return;
            }

            int[] coords = GetMouseCoords();

            int mouseX = coords[0];
            int mouseY = coords[1];

            if (clicking != ColourSelection.NONE)
            {
                switch (selectedTool)
                {
                    case Tool.PENCIL:
                        image.SetPixel(mouseX / scale, mouseY / scale, selectedColour[(int)clicking]);
                        break;
                    case Tool.LINE:
                        image.DrawLine(startX / scale, startY / scale, mouseX / scale, mouseY / scale, selectedColour[(int)clicking]);
                        break;
                    case Tool.RECT:
                        image.DrawRect(startX / scale, startY / scale, mouseX / scale, mouseY / scale, selectedColour[(int)clicking]);
                        break;
                    case Tool.ELLIPSES:
                        image.DrawEllipse(Math.Abs(mouseX / scale - startX / scale), Math.Abs(mouseY / scale - startY / scale), startX / scale, startY / scale, selectedColour[(int)clicking]);
                        break;
                    default:
                        break;
                }

                UpdateState();
            }
        }

        private async void UpdateState()
        {
            if (image == null)
            {
                return;
            }

            bmp.Lock();

            await Task.Run(() =>
            {
                Int32Rect rect = new()
                {
                    X = 0,
                    Y = 0,
                    Width = image.Width,
                    Height = image.Height
                };

                byte[] colour = new byte[rect.Width * rect.Height * 4];

                for (int y = 0; y < image.Height; y++)
                {
                    for (int x = 0; x < image.Width; x++)
                    {
                        colour[(x + y * rect.Width) * ImageData.COLOUR_CHANNELS + (int)ImageData.ColourChannelIndex.BLUE] = image.Palette[image.Data[x + y * image.Width]][(int)ImageData.ColourChannelIndex.BLUE];
                        colour[(x + y * rect.Width) * ImageData.COLOUR_CHANNELS + (int)ImageData.ColourChannelIndex.GREEN] = image.Palette[image.Data[x + y * image.Width]][(int)ImageData.ColourChannelIndex.GREEN];
                        colour[(x + y * rect.Width) * ImageData.COLOUR_CHANNELS + (int)ImageData.ColourChannelIndex.RED] = image.Palette[image.Data[x + y * image.Width]][(int)ImageData.ColourChannelIndex.RED];
                        colour[(x + y * rect.Width) * ImageData.COLOUR_CHANNELS + (int)ImageData.ColourChannelIndex.ALPHA] = image.Palette[image.Data[x + y * image.Width]][(int)ImageData.ColourChannelIndex.ALPHA];
                    }
                }


                Application.Current.Dispatcher.BeginInvoke(() => bmp.WritePixels(rect, colour, rect.Width * ImageData.COLOUR_CHANNELS, 0));
            });

            bmp.Unlock();

            UpdateHistoryPerms();
        }

        private void ZoomOutBtn_Click(object sender, RoutedEventArgs e)
        {
            if (zoomLevel - 1 < MIN_ZOOM_LEVEL)
            {
                zoomLevel = MIN_ZOOM_LEVEL;
            } else
            {
                zoomLevel--;
            }

            ZoomLvText.Content = $"X{(int)Math.Pow(2, zoomLevel - 1)}";
            ResizeCanvas();
        }

        private void ZoomInBtn_Click(object sender, RoutedEventArgs e)
        {
            if (zoomLevel + 1 > MAX_ZOOM_LEVEL)
            {
                zoomLevel = MAX_ZOOM_LEVEL;
            }
            else
            {
                zoomLevel++;
            }

            ZoomLvText.Content = $"X{(int)Math.Pow(2, zoomLevel - 1)}";
            ResizeCanvas();
        }

        private void ResizeCanvas()
        {
            if (image == null)
            {
                return;
            }

            Canvas.Width = (CANVAS_BASE_WIDTH / canvasWidthAspect) * Math.Pow(2, zoomLevel - 1);
            Canvas.Height = (CANVAS_BASE_HEIGHT / canvasHeightAspect) * Math.Pow(2, zoomLevel - 1);

            CanvasSurface.Width = Canvas.Width;
            CanvasSurface.Height = Canvas.Height;

            GridDisplay.Width = Canvas.Width;
            GridDisplay.Height = Canvas.Height;

            SetupGrid(image.Width, image.Height, zoomLevel, GRID_SIZE);

            scale = (int) (Canvas.Width / image.Width);

            UpdateState();
        }

        private void Canvas_MouseClick(object sender, MouseButtonEventArgs e)
        {
            if (image == null)
            {
                return;
            }

            int[] coords = GetMouseCoords();

            int mouseX = coords[0];
            int mouseY = coords[1];

            undoStack.Push(new DrawAction(image));

            redoStack.Clear();

            switch (e.ChangedButton)
            {
                case MouseButton.Left:
                    clicking = ColourSelection.PRIMARY;
                    break;
                case MouseButton.Right:
                    clicking = ColourSelection.SECONDARY;
                    break;
                default:
                    clicking = ColourSelection.NONE;
                    break;
            }

            if (clicking != ColourSelection.NONE)
            {
                switch (selectedTool)
                {
                    case Tool.PENCIL:
                        image.SetPixel(mouseX / scale, mouseY / scale, selectedColour[(int)clicking]);
                        break;
                    case Tool.FILL:
                        image.FloodFill(mouseX / scale, mouseY / scale, selectedColour[(int)clicking]);
                        break;
                    case Tool.LINE:
                        startX = mouseX;
                        startY = mouseY;

                        image.DrawLine(startX / scale, startY / scale, mouseX / scale, mouseY / scale, selectedColour[(int)clicking]);
                        break;
                    case Tool.RECT:
                        startX = mouseX;
                        startY = mouseY;

                        image.DrawRect(startX / scale, startY / scale, mouseX / scale, mouseY / scale, selectedColour[(int)clicking]);
                        break;
                    case Tool.ELLIPSES:
                        startX = mouseX;
                        startY = mouseY;

                        image.DrawEllipse(1, 1, startX / scale, startY / scale, selectedColour[(int)clicking]);
                        break;
                    default:
                        break;
                }
            }

            UpdateState();
        }

        private void Canvas_MouseRelease(object sender, MouseButtonEventArgs e)
        {
            if (image == null)
            {
                return;
            }

            int[] coords = GetMouseCoords();

            int mouseX = coords[0];
            int mouseY = coords[1];

            int lastClick = (int)clicking;

            clicking = ColourSelection.NONE;

            if (lastClick == (int)ColourSelection.NONE)
            {
                return;
            }

            switch (selectedTool)
            {
                case Tool.LINE:
                    image.DrawLine(startX / scale, startY / scale, mouseX / scale, mouseY / scale, selectedColour[lastClick], true);
                    break;
                case Tool.RECT:
                    image.DrawRect(startX / scale, startY / scale, mouseX / scale, mouseY / scale, selectedColour[lastClick], true);
                    break;
                case Tool.ELLIPSES:
                    image.DrawEllipse(Math.Abs(mouseX / scale - startX / scale), Math.Abs(mouseY / scale - startY / scale), startX / scale, startY / scale, selectedColour[lastClick], true);
                    break;
                default:
                    break;
            }

            startX = 0;
            startY = 0;
            
            UpdateState();
        }

        private int[] GetMouseCoords()
        {
            int[] coords = [
                (int)Mouse.GetPosition(Canvas).X,
                (int)Mouse.GetPosition(Canvas).Y
            ];

            return coords;
        }

        private void SliderValue_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            int r = (int)RedSlider.Value;
            int g = (int)GreenSlider.Value;
            int b = (int)BlueSlider.Value;

            string hexCode = $"{r.ToString("X2").ToUpper()}{g.ToString("X2").ToUpper()}{b.ToString("X2").ToUpper()}";

            HexValueText.Text = $"#{hexCode}";
        }

        private void HexValue_Changed(object sender, KeyEventArgs e)
        {
            if (e.Key != Key.Enter)
            {
                return;
            }

            int[] value = Utilities.GetHexcodeFromString(HexValueText.Text);

            RedSlider.Value = value[0];
            GreenSlider.Value = value[1];
            BlueSlider.Value = value[2];
        }

        private void UpdateColour(int r, int g, int b)
        {
            if (image == null)
            {
                return;
            }

            //undoStack.Push(new PaletteAction(image));
            //redoStack.Clear();

            image.Palette[selectedColour[(int)ColourSelection.PRIMARY]][0] = (byte)b;
            image.Palette[selectedColour[(int)ColourSelection.PRIMARY]][1] = (byte)g;
            image.Palette[selectedColour[(int)ColourSelection.PRIMARY]][2] = (byte)r;
            image.Palette[selectedColour[(int)ColourSelection.PRIMARY]][3] = 0xff;

            SolidColorBrush brush = new(System.Windows.Media.Color.FromArgb(0xff, (byte)r, (byte)g, (byte)b));

            SelectedColour.Background = brush;
            swatches[selectedColour[0]].Background = brush;

            if (selectedColour[0] == selectedColour[1])
            {
                SelectedAltColour.Background = brush;
            }

            UpdateState();
        }

        private void SaveBtn_Click(object sender, RoutedEventArgs e)
        {
            SaveImage();
        }

        private void SaveAsBtn_Click(object sender, RoutedEventArgs e)
        {
            SaveImage(false);
        }

        private void LoadBtn_Click(object sender, RoutedEventArgs e)
        {
            if (!SavePrompt())
            {
                return;
            }

            OpenFileDialog openFileDialog = new()
            {
                DefaultExt = ".dlv.bmp",
                Filter = "Dotsweeper Level Files|*.dlv.bmp|Dotsweeper Extended Level Files|*.dlvx.bmp",
                AddExtension = true,
                CheckFileExists = true
            };

            if (openFileDialog.ShowDialog(this) == true)
            {
                string url = openFileDialog.FileName;

                image = SaveManager.LoadImageFromDisk(url);

                if (image != null)
                {
                    CalculateCanvasAspect(image.Width, image.Height);
                    SetupEditor(url);
                }
                else
                {
                    MessageBox.Show(this, "Error loading the image file.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void UpdatePaletteUI()
        {
            if (image == null)
            {
                return;
            }

            for (int i = 0; i < image.Palette.Length; i++)
            {
                byte a = image.Palette[i][(int)ImageData.ColourChannelIndex.ALPHA];
                byte r = image.Palette[i][(int)ImageData.ColourChannelIndex.RED];
                byte g = image.Palette[i][(int)ImageData.ColourChannelIndex.GREEN];
                byte b = image.Palette[i][(int)ImageData.ColourChannelIndex.BLUE];

                swatches[i].Background = new SolidColorBrush(System.Windows.Media.Color.FromArgb(a, r, g, b));
            }

            UpdateSelectedColour(ColourSelection.PRIMARY);
            UpdateSelectedColour(ColourSelection.SECONDARY);

            //UpdateHistoryPerms();
        }

        private void LoadPaletteBtn_Click(object sender, RoutedEventArgs e)
        {
            if (image == null)
            {
                return;
            }

            OpenFileDialog openFileDialog = new()
            {
                DefaultExt = ".hex",
                Filter = "Hex Colour Files|*.hex",
                AddExtension = true,
                CheckFileExists = true,
                DefaultDirectory = "Palettes"
            };

            if (openFileDialog.ShowDialog(this) == true)
            {
                if (PaletteManager.LoadPalette(image.Palette, openFileDialog.FileName))
                {
                    UpdatePaletteUI();
                }
            }
        }

        private void SavePaletteBtn_Click(object sender, RoutedEventArgs e)
        {
            SavePalette();
        }

        private bool SaveImage(bool overwrite = true)
        {
            if (image == null)
            {
                return false;
            }

            string format = image.GetFormatIdentifier();

            SaveFileDialog saveFileDialog = new()
            {
                DefaultExt = (format == "DLV1") ? ".dlv.bmp" : ".dlvx.bmp",
                Filter = (format == "DLV1") ? "Dotsweeper Level Files|*.dlv.bmp" : "Dotsweeper Expanded Level Files|*.dlvx.bmp",
                AddExtension = true,
                OverwritePrompt = true
            };

            bool confirm = true;

            if (fileURL == "" || !overwrite)
            {
                confirm = saveFileDialog.ShowDialog(this) == true;

                if (confirm)
                {
                    fileURL = saveFileDialog.FileName;
                }
            }

            if (confirm)
            {
                if (SaveManager.SaveImageToDisk(fileURL, image))
                {
                    MessageBox.Show(this, "Image saved", "Save", MessageBoxButton.OK, MessageBoxImage.Information);
                    return true;
                }
                else
                {
                    MessageBox.Show(this, $"Error saving file.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return false;
                }
            }

            return false;
        }

        private void SavePalette()
        {
            if (image == null)
            {
                return;
            }
            
            SaveFileDialog saveFileDialog = new()
            {
                DefaultExt = ".hex",
                Filter = "Hex Colour Files|*.hex",
                AddExtension = true,
                OverwritePrompt = true
            };

            if (saveFileDialog.ShowDialog(this) == true)
            {
                string url = saveFileDialog.FileName;

                if (PaletteManager.SavePalette(url, image.Palette))
                {
                    MessageBox.Show(this, "Palette saved.", "Save", MessageBoxButton.OK, MessageBoxImage.Information);
                } else
                {
                    MessageBox.Show(this, "Cannot save palette file.", "Error creating file", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void CloseBtn_Click(object sender, RoutedEventArgs e)
        {
            if (SavePrompt())
            {
                image = null;

                ChangeControlState(false);
            }
        }

        private void ExitBtn_Click(object sender, RoutedEventArgs e)
        {
            Exit();
        }

        private bool Exit()
        {
            if (SavePrompt())
            {
                Environment.Exit(0);
            }

            return true;
        }

        private bool SavePrompt()
        {
            if (image != null)
            {
                MessageBoxResult res = MessageBox.Show(this, "Save changes?", "Save", MessageBoxButton.YesNoCancel, MessageBoxImage.Question);

                if (res == MessageBoxResult.Cancel)
                {
                    return false;
                }
                else if (res == MessageBoxResult.Yes)
                {
                    return SaveImage();
                }
            }

            return true;
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            e.Cancel = Exit();
        }

        private void ChangeColourBtn_Click(object sender, RoutedEventArgs e)
        {
            int r = (int)RedSlider.Value;
            int g = (int)GreenSlider.Value;
            int b = (int)BlueSlider.Value;

            UpdateColour(r, g, b);
        }

        private void Undo()
        {
            if (undoStack.Count > 0 && clicking == ColourSelection.NONE)
            {
                IAction action = undoStack.Pop();
                redoStack.Push(action.ExecuteAction());

                UpdateState();
            }
        }

        private void Redo()
        {
            if (redoStack.Count > 0 && clicking == ColourSelection.NONE)
            {
                IAction action = redoStack.Pop();
                undoStack.Push(action.ExecuteAction());

                UpdateState();
            }
        }

        private void UndoBtn_Click(object sender, RoutedEventArgs e)
        {
            Undo();
        }

        private void RedoBtn_Click(object sender, RoutedEventArgs e)
        {
            Redo();
        }

        private void UpdateHistoryPerms()
        {
            UndoBtn.IsEnabled = undoStack.Count > 0;
            RedoBtn.IsEnabled = redoStack.Count > 0;
            UndoToolBtn.IsEnabled = UndoBtn.IsEnabled;
            RedoToolBtn.IsEnabled = RedoBtn.IsEnabled;
        }

        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Z && (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
            {
                if (undoStack.Count > 0)
                {
                    Undo();
                }
                else
                {
                    SystemSounds.Beep.Play();
                }
            } else if (e.Key == Key.Y && (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
            {
                if (redoStack.Count > 0)
                {
                    Redo();
                }
                else
                {
                    SystemSounds.Beep.Play();
                }
            } else if (e.Key == Key.S && (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control && clicking == ColourSelection.NONE)
            {
                if (SaveBtn.IsEnabled)
                {
                    SaveImage();
                }
                else
                {
                    SystemSounds.Beep.Play();
                }
            }
        }

        private void ToolBtn_Click(object sender, RoutedEventArgs e)
        {
            currentTool.Tag = "";
            currentTool = (Button)sender;
            currentTool.Tag = "Active";

            switch (currentTool.Name)
            {
                case "PencilBtn":
                    selectedTool = Tool.PENCIL;
                    break;
                case "FillBtn":
                    selectedTool = Tool.FILL;
                    break;
                case "LineBtn":
                    selectedTool = Tool.LINE;
                    break;
                case "RectBtn":
                    selectedTool = Tool.RECT;
                    break;
                case "EllipseBtn":
                    selectedTool = Tool.ELLIPSES;
                    break;
                default:
                    selectedTool = Tool.PENCIL;
                    break;
            }
        }

        private void GridEnabled_Click(object sender, RoutedEventArgs e)
        {
            GridDisplay.ShowGridLines = GridEnabled.IsChecked;
        }

        private void About_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show(this, "DotEditor v1.0.0\n\nA simple pixel art editor with DotSweeper support.\n\n(C) QLYCO / dfx", "About");
        }

        private void ManualBtn_Click(object sender, RoutedEventArgs e)
        {
            Process.Start(new ProcessStartInfo { FileName = "https://qlycoworks.com/dotsweeper", UseShellExecute = true });
        }
    }
}