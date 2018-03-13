using System;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Input;

namespace BugShooting.Output.BugTrackerNet
{
  partial class Send : Window
  {
 
    public Send(string url, int lastBugID, string fileName)
    {
      InitializeComponent();

      Url.Text = url;
      NewBug.IsChecked = true;
      BugIDTextBox.Text = lastBugID.ToString();
      FileNameTextBox.Text = fileName;

      BugIDTextBox.TextChanged += ValidateData;
      FileNameTextBox.TextChanged += ValidateData;
      ValidateData(null, null);

    }

    public bool CreateNewBug
    {
      get { return NewBug.IsChecked.Value; }
    }
 
    public string Comment
    {
      get { return CommentTextBox.Text; }
    }

    public string BugID
    {
      get { return BugIDTextBox.Text; }
    }

    public string FileName
    {
      get { return FileNameTextBox.Text; }
    }

    private void NewBug_CheckedChanged(object sender, EventArgs e)
    {

      if (NewBug.IsChecked.Value)
      {
        CommentTextBox.SelectAll();
        CommentTextBox.Focus();
      }
      else
      {   
        BugIDTextBox.SelectAll();
        BugIDTextBox.Focus();
      }

      ValidateData(null, null);

    }

    private void BugIDTextBox_GotFocus(object sender, RoutedEventArgs e)
    {
      AttachToBug.IsChecked = true;
    }

    private void BugID_PreviewTextInput(object sender, TextCompositionEventArgs e)
    {
      e.Handled = Regex.IsMatch(e.Text, "[^0-9]+");
    }
    
    private void ValidateData(object sender, EventArgs e)
    {
      OK.IsEnabled = (CreateNewBug || Validation.IsValid(BugIDTextBox)) &&
                     Validation.IsValid(FileNameTextBox);
    }

    private void OK_Click(object sender, RoutedEventArgs e)
    {
      this.DialogResult = true;
    }

  }

}
