using System.Windows;

namespace VRTDoubao.UI;

public partial class HelpWindow : Window
{
    public HelpWindow(string instructions)
    {
        InitializeComponent();
        TbHelp.Text = instructions;
    }
}


