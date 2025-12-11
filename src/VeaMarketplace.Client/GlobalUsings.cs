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
global using Color = System.Windows.Media.Color;
global using KeyEventArgs = System.Windows.Input.KeyEventArgs;
global using MessageBox = System.Windows.MessageBox;
global using Orientation = System.Windows.Controls.Orientation;
global using Rectangle = System.Windows.Shapes.Rectangle;
global using UserControl = System.Windows.Controls.UserControl;
