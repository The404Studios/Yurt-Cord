// Global using directives to resolve ambiguous type references
// between System.Windows (WPF) and System.Windows.Forms namespaces

// Explicitly use WPF namespaces as the defaults
global using System;
global using System.Collections.Generic;
global using System.IO;
global using System.Linq;
global using System.Threading;
global using System.Threading.Tasks;

// WPF-specific type aliases to resolve ambiguities
global using Application = System.Windows.Application;
global using Binding = System.Windows.Data.Binding;
global using Brush = System.Windows.Media.Brush;
global using Brushes = System.Windows.Media.Brushes;
global using Button = System.Windows.Controls.Button;
global using Clipboard = System.Windows.Clipboard;
global using Color = System.Windows.Media.Color;
global using ColorConverter = System.Windows.Media.ColorConverter;
global using Cursors = System.Windows.Input.Cursors;
global using HorizontalAlignment = System.Windows.HorizontalAlignment;
global using KeyEventArgs = System.Windows.Input.KeyEventArgs;
global using MouseEventArgs = System.Windows.Input.MouseEventArgs;
global using MessageBox = System.Windows.MessageBox;
global using OpenFileDialog = Microsoft.Win32.OpenFileDialog;
global using Orientation = System.Windows.Controls.Orientation;
global using Point = System.Windows.Point;
global using Rectangle = System.Windows.Shapes.Rectangle;
global using RadioButton = System.Windows.Controls.RadioButton;
global using TextBox = System.Windows.Controls.TextBox;
global using UserControl = System.Windows.Controls.UserControl;
global using VerticalAlignment = System.Windows.VerticalAlignment;
