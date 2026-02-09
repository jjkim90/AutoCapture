using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace AutoCapture.Wpf.Controls
{
    public class KeySelectionControl : UserControl
    {
        public static readonly DependencyProperty SelectedKeyProperty =
            DependencyProperty.Register(nameof(SelectedKey), typeof(string), typeof(KeySelectionControl),
                new FrameworkPropertyMetadata(string.Empty, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

        public string SelectedKey
        {
            get => (string)GetValue(SelectedKeyProperty);
            set => SetValue(SelectedKeyProperty, value);
        }

        private TextBox _displayTextBox;
        private Button _customKeyButton;
        private Button _spaceButton;
        private Button _rightArrowButton;

        public KeySelectionControl()
        {
            CreateContent();
        }

        private void CreateContent()
        {
            var grid = new Grid();
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            // 설명 텍스트
            var descriptionText = new TextBlock
            {
                Text = "캡처 후 자동으로 입력할 키를 선택하세요",
                Style = Application.Current.FindResource("MaterialDesignBody2TextBlock") as Style,
                Margin = new Thickness(0, 0, 0, 8)
            };
            Grid.SetRow(descriptionText, 0);
            grid.Children.Add(descriptionText);

            // 선택된 키 표시 (읽기전용)
            _displayTextBox = new TextBox
            {
                IsReadOnly = true,
                Style = Application.Current.FindResource("MaterialDesignFloatingHintTextBox") as Style,
                Margin = new Thickness(0, 0, 0, 12)
            };
            
            // HintAssist 설정
            var hintAssist = typeof(MaterialDesignThemes.Wpf.HintAssist);
            var hintProperty = hintAssist.GetField("HintProperty")?.GetValue(null) as DependencyProperty;
            if (hintProperty != null)
            {
                _displayTextBox.SetValue(hintProperty, "선택된 키가 여기에 표시됩니다");
            }

            _displayTextBox.SetBinding(TextBox.TextProperty, new System.Windows.Data.Binding(nameof(SelectedKey))
            {
                Source = this,
                Mode = System.Windows.Data.BindingMode.OneWay
            });

            Grid.SetRow(_displayTextBox, 1);
            grid.Children.Add(_displayTextBox);

            // 버튼 패널
            var buttonPanel = new StackPanel { Orientation = Orientation.Horizontal };

            // 커스텀 키 입력 버튼
            _customKeyButton = new Button
            {
                Content = "커스텀 키 입력",
                Style = Application.Current.FindResource("MaterialDesignRaisedButton") as Style,
                Margin = new Thickness(0, 0, 8, 0)
            };
            _customKeyButton.Click += OnCustomKeyButtonClick;
            buttonPanel.Children.Add(_customKeyButton);

            // Space 키 버튼
            _spaceButton = new Button
            {
                Content = "Space",
                Style = Application.Current.FindResource("MaterialDesignOutlinedButton") as Style,
                Margin = new Thickness(0, 0, 8, 0)
            };
            _spaceButton.Click += (s, e) => SelectedKey = "Space";
            buttonPanel.Children.Add(_spaceButton);

            // 우측 화살표 키 버튼
            _rightArrowButton = new Button
            {
                Content = "→",
                Style = Application.Current.FindResource("MaterialDesignOutlinedButton") as Style,
                ToolTip = "오른쪽 화살표 키"
            };
            _rightArrowButton.Click += (s, e) => SelectedKey = "Right";
            buttonPanel.Children.Add(_rightArrowButton);

            Grid.SetRow(buttonPanel, 2);
            grid.Children.Add(buttonPanel);

            Content = grid;
        }

        private void OnCustomKeyButtonClick(object sender, RoutedEventArgs e)
        {
            var window = new KeyCaptureWindow();
            window.Owner = Window.GetWindow(this);
            if (window.ShowDialog() == true)
            {
                SelectedKey = window.CapturedKey;
            }
        }
    }
}