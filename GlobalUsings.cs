// Global using aliases to resolve WPF vs WinForms namespace collisions.
// When both UseWPF and UseWindowsForms are enabled, many types exist
// in both System.Windows and System.Windows.Forms namespaces.

global using System.IO;
global using Application = System.Windows.Application;
global using DataObject = System.Windows.DataObject;
global using DragDropEffects = System.Windows.DragDropEffects;
global using DragEventArgs = System.Windows.DragEventArgs;
global using MouseEventArgs = System.Windows.Input.MouseEventArgs;
global using Point = System.Windows.Point;
global using UserControl = System.Windows.Controls.UserControl;
global using Clipboard = System.Windows.Clipboard;
global using MessageBox = System.Windows.MessageBox;
global using FileAttributes = System.IO.FileAttributes;
