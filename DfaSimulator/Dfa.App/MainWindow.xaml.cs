using System.Data;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using Dfa.Core;
using Microsoft.Win32;

namespace Dfa.App;

public partial class MainWindow : Window
{
    private DfaDocument _currentDoc = new();
    private DfaRun? _currentRun;
    private readonly DispatcherTimer _playTimer = new();

    // Variabile pentru interacțiunea cu mouse-ul pe Canvas
    private string? _selectedState;
    private string? _draggingState;
    private Point _dragOffset;
    private string? _transitionSourceState;
    private const double StateRadius = 28.0;

    // Document dedicat pentru Tab-ul 2 (Numere de înmatriculare)
    private readonly DfaDocument _licensePlateDoc = PredefinedExamples.CreateRomanianLicensePlate();

    public MainWindow()
    {
        InitializeComponent();

        _playTimer.Interval = TimeSpan.FromMilliseconds(700);
        _playTimer.Tick += PlayTimer_Tick;

        CmbExamples.ItemsSource = new[]
        {
            "1. Număr par de 'a'",
            "5. Numere binare divizibile cu 3",
            "A1. Număr de înmatriculare RO",
            "Automat gol (construiește de la zero)"
        };
        CmbExamples.SelectedIndex = 0;

        ValidateLicensePlateLive();
    }

    // =========================================================================
    // 1. ÎNCĂRCARE EXEMPLE ȘI FIȘIERE JSON
    // =========================================================================
    private void CmbExamples_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        _playTimer.Stop();
        BtnPlayPause.Content = "▶ Play Automat";

        switch (CmbExamples.SelectedIndex)
        {
            case 0:
                LoadDocument(PredefinedExamples.CreateEvenA(), "abba");
                break;
            case 1:
                LoadDocument(PredefinedExamples.CreateBinaryDivisibleBy3(), "110");
                break;
            case 2:
                LoadDocument(PredefinedExamples.CreateRomanianLicensePlate(), "B 123 ABC");
                break;
            case 3:
                var empty = new DfaDocument { Title = "Automat Nou" };
                empty.Dfa.Alphabet.UnionWith(["a", "b"]);
                LoadDocument(empty, "");
                break;
        }
    }

    private void LoadDocument(DfaDocument doc, string defaultWord)
    {
        _currentDoc = doc;
        _selectedState = null;
        _transitionSourceState = null;

        TxtAlphabet.Text = string.Join(",", _currentDoc.Dfa.Alphabet);
        TxtInputWord.Text = defaultWord;

        UpdateSelectedStatePanel();
        UpdateDeltaTable();
        RunBatchTests();
        StartNewSimulation(false);
    }

    private void BtnSaveJson_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new SaveFileDialog
        {
            Filter = "Fișier AFD JSON (*.dfa.json)|*.dfa.json|Toate fișierele (*.*)|*.*",
            FileName = "automat.dfa.json"
        };
        if (dlg.ShowDialog() == true)
        {
            File.WriteAllText(dlg.FileName, _currentDoc.ToJson());
            MessageBox.Show("Automatul a fost salvat cu succes!", "Salvare JSON", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }

    private void BtnLoadJson_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog
        {
            Filter = "Fișier AFD JSON (*.dfa.json;*.json)|*.dfa.json;*.json|Toate fișierele (*.*)|*.*"
        };
        if (dlg.ShowDialog() == true)
        {
            try
            {
                string json = File.ReadAllText(dlg.FileName);
                var doc = DfaDocument.FromJson(json);
                string firstWord = doc.TestSuite.FirstOrDefault()?.Word ?? "";
                LoadDocument(doc, firstWord);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Fișierul JSON nu a putut fi încărcat:\n{ex.Message}", "Eroare la încărcare", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    // =========================================================================
    // 2. DESENAREA AUTOMATULUI PE CANVAS (CERCURI, SĂGEȚI, BUCLE)
    // =========================================================================
    private void RedrawCanvas()
    {
        DfaCanvas.Children.Clear();

        string? activeState = _currentRun?.CurrentStep.CurrentState;
        string? prevState = _currentRun?.CurrentStep.StateBefore;
        string? activeSymbol = _currentRun?.CurrentStep.MatchedSymbol;

        // Asigurăm că fiecare stare are coordonate pe ecran
        int autoIdx = 0;
        foreach (var s in _currentDoc.Dfa.States)
        {
            if (!_currentDoc.StateLayout.ContainsKey(s))
            {
                _currentDoc.StateLayout[s] = new StateVisualInfo(120 + (autoIdx % 5) * 160, 120 + (autoIdx / 5) * 140);
                autoIdx++;
            }
        }

        // Grupăm tranzițiile care au aceeași sursă și aceeași destinație (ex: "a, b")
        var groupedTransitions = _currentDoc.Dfa.Transitions
            .GroupBy(t => (t.FromState, t.ToState))
            .ToList();

        // 1. Desenăm săgețile (tranzițiile)
        foreach (var group in groupedTransitions)
        {
            string from = group.Key.FromState;
            string to = group.Key.ToState;
            if (!_currentDoc.StateLayout.ContainsKey(from) || !_currentDoc.StateLayout.ContainsKey(to))
                continue;

            string label = string.Join(", ", group.Select(x => x.Symbol));
            bool isActiveEdge = prevState == from && activeState == to && activeSymbol != null && group.Any(x => x.Symbol == activeSymbol);

            var p1 = new Point(_currentDoc.StateLayout[from].X, _currentDoc.StateLayout[from].Y);
            var p2 = new Point(_currentDoc.StateLayout[to].X, _currentDoc.StateLayout[to].Y);

            if (from == to)
            {
                DrawSelfLoop(p1, label, isActiveEdge);
            }
            else
            {
                bool hasReverse = groupedTransitions.Any(g => g.Key.FromState == to && g.Key.ToState == from);
                DrawDirectedEdge(p1, p2, label, hasReverse, isActiveEdge);
            }
        }

        // 2. Desenăm stările (cercurile)
        foreach (var state in _currentDoc.Dfa.States)
        {
            var info = _currentDoc.StateLayout[state];
            var center = new Point(info.X, info.Y);

            bool isInitial = _currentDoc.Dfa.InitialState == state;
            bool isFinal = _currentDoc.Dfa.FinalStates.Contains(state);
            bool isCurrentSim = activeState == state;
            bool isSelected = _selectedState == state || _transitionSourceState == state;

            // Săgeata de intrare pentru starea inițială
            if (isInitial)
            {
                DrawInitialArrow(center);
            }

            // Cercul exterior
            var circle = new Ellipse
            {
                Width = StateRadius * 2,
                Height = StateRadius * 2,
                Fill = isCurrentSim ? new SolidColorBrush(Color.FromRgb(253, 241, 220))
                     : isFinal ? new SolidColorBrush(Color.FromRgb(227, 244, 234))
                     : Brushes.White,
                Stroke = isCurrentSim ? new SolidColorBrush(Color.FromRgb(154, 91, 0))
                       : isSelected ? new SolidColorBrush(Color.FromRgb(47, 91, 211))
                       : isFinal ? new SolidColorBrush(Color.FromRgb(31, 122, 76))
                       : new SolidColorBrush(Color.FromRgb(29, 31, 35)),
                StrokeThickness = (isCurrentSim || isSelected) ? 3.2 : 1.8
            };
            Canvas.SetLeft(circle, center.X - StateRadius);
            Canvas.SetTop(circle, center.Y - StateRadius);
            DfaCanvas.Children.Add(circle);

            // Al doilea cerc interior pentru stările finale
            if (isFinal)
            {
                double innerR = StateRadius - 5;
                var innerCircle = new Ellipse
                {
                    Width = innerR * 2,
                    Height = innerR * 2,
                    Stroke = isCurrentSim ? new SolidColorBrush(Color.FromRgb(154, 91, 0)) : new SolidColorBrush(Color.FromRgb(31, 122, 76)),
                    StrokeThickness = 1.4,
                    IsHitTestVisible = false
                };
                Canvas.SetLeft(innerCircle, center.X - innerR);
                Canvas.SetTop(innerCircle, center.Y - innerR);
                DfaCanvas.Children.Add(innerCircle);
            }

            // Numele stării (ex: q0)
            var lbl = new TextBlock
            {
                Text = state,
                FontWeight = FontWeights.Bold,
                FontSize = 13,
                Foreground = new SolidColorBrush(Color.FromRgb(29, 31, 35)),
                IsHitTestVisible = false
            };
            lbl.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            Canvas.SetLeft(lbl, center.X - lbl.DesiredSize.Width / 2);
            Canvas.SetTop(lbl, center.Y - lbl.DesiredSize.Height / 2);
            DfaCanvas.Children.Add(lbl);

            // Descrierea scurtă sub stare (SI-10)
            if (!string.IsNullOrWhiteSpace(info.Description))
            {
                var descBlock = new TextBlock
                {
                    Text = info.Description,
                    FontSize = 10.5,
                    Foreground = new SolidColorBrush(Color.FromRgb(93, 99, 110)),
                    MaxWidth = 130,
                    TextWrapping = TextWrapping.Wrap,
                    TextAlignment = TextAlignment.Center,
                    IsHitTestVisible = false
                };
                descBlock.Measure(new Size(130, double.PositiveInfinity));
                Canvas.SetLeft(descBlock, center.X - descBlock.DesiredSize.Width / 2);
                Canvas.SetTop(descBlock, center.Y + StateRadius + 4);
                DfaCanvas.Children.Add(descBlock);
            }
        }
    }

    private void DrawInitialArrow(Point center)
    {
        Point start = new(center.X - StateRadius - 38, center.Y);
        Point end = new(center.X - StateRadius - 2, center.Y);

        var line = new Line
        {
            X1 = start.X,
            Y1 = start.Y,
            X2 = end.X,
            Y2 = end.Y,
            Stroke = new SolidColorBrush(Color.FromRgb(47, 91, 211)),
            StrokeThickness = 2
        };
        DfaCanvas.Children.Add(line);
        DrawArrowhead(start, end, new SolidColorBrush(Color.FromRgb(47, 91, 211)));
    }

    private void DrawSelfLoop(Point center, string label, bool isActive)
    {
        Brush stroke = isActive ? new SolidColorBrush(Color.FromRgb(154, 91, 0)) : new SolidColorBrush(Color.FromRgb(60, 64, 72));
        double thickness = isActive ? 2.8 : 1.5;

        Point start = new(center.X - 12, center.Y - StateRadius + 2);
        Point end = new(center.X + 12, center.Y - StateRadius + 2);
        Point ctrl1 = new(center.X - 30, center.Y - StateRadius - 42);
        Point ctrl2 = new(center.X + 30, center.Y - StateRadius - 42);

        var fig = new PathFigure { StartPoint = start };
        fig.Segments.Add(new BezierSegment(ctrl1, ctrl2, end, true));
        var geo = new PathGeometry();
        geo.Figures.Add(fig);

        var path = new System.Windows.Shapes.Path { Data = geo, Stroke = stroke, StrokeThickness = thickness };
        DfaCanvas.Children.Add(path);

        DrawArrowhead(ctrl2, end, stroke);
        DrawEdgeLabel(new Point(center.X, center.Y - StateRadius - 38), label, isActive);
    }

    private void DrawDirectedEdge(Point p1, Point p2, string label, bool curve, bool isActive)
    {
        Brush stroke = isActive ? new SolidColorBrush(Color.FromRgb(154, 91, 0)) : new SolidColorBrush(Color.FromRgb(60, 64, 72));
        double thickness = isActive ? 2.8 : 1.5;

        Vector dir = p2 - p1;
        if (dir.Length < 1) return;
        dir.Normalize();
        Vector perp = new(-dir.Y, dir.X);

        double offset = curve ? 26.0 : 0.0;
        Point mid = new((p1.X + p2.X) / 2 + perp.X * offset, (p1.Y + p2.Y) / 2 + perp.Y * offset);

        Vector startDir = mid - p1; startDir.Normalize();
        Vector endDir = p2 - mid; endDir.Normalize();

        Point start = p1 + startDir * StateRadius;
        Point end = p2 - endDir * StateRadius;

        var fig = new PathFigure { StartPoint = start };
        fig.Segments.Add(new QuadraticBezierSegment(mid, end, true));
        var geo = new PathGeometry();
        geo.Figures.Add(fig);

        var path = new System.Windows.Shapes.Path { Data = geo, Stroke = stroke, StrokeThickness = thickness };
        DfaCanvas.Children.Add(path);

        DrawArrowhead(mid, end, stroke);

        Point labelPos = new((start.X + 2 * mid.X + end.X) / 4, (start.Y + 2 * mid.Y + end.Y) / 4 - 10);
        DrawEdgeLabel(labelPos, label, isActive);
    }

    private void DrawArrowhead(Point from, Point to, Brush brush)
    {
        Vector dir = to - from;
        if (dir.Length < 0.1) return;
        dir.Normalize();
        Vector perp = new(-dir.Y, dir.X);

        Point p1 = to - dir * 10 + perp * 5;
        Point p2 = to - dir * 10 - perp * 5;

        var poly = new Polygon
        {
            Points = new PointCollection { to, p1, p2 },
            Fill = brush
        };
        DfaCanvas.Children.Add(poly);
    }

    private void DrawEdgeLabel(Point pos, string text, bool isActive)
    {
        var border = new Border
        {
            Background = isActive ? new SolidColorBrush(Color.FromRgb(253, 241, 220)) : Brushes.White,
            BorderBrush = isActive ? new SolidColorBrush(Color.FromRgb(154, 91, 0)) : Brushes.Transparent,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(4),
            Padding = new Thickness(4, 1, 4, 1),
            IsHitTestVisible = false,
            Child = new TextBlock
            {
                Text = text,
                FontWeight = FontWeights.Bold,
                FontSize = 12,
                Foreground = isActive ? new SolidColorBrush(Color.FromRgb(154, 91, 0)) : new SolidColorBrush(Color.FromRgb(47, 91, 211))
            }
        };
        border.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        Canvas.SetLeft(border, pos.X - border.DesiredSize.Width / 2);
        Canvas.SetTop(border, pos.Y - border.DesiredSize.Height / 2);
        DfaCanvas.Children.Add(border);
    }

    // =========================================================================
    // 3. INTERACȚIUNEA CU MOUSE-UL (EDITORUL GRAFIC: ED-01 .. ED-05)
    // =========================================================================
    private string? GetStateAtPoint(Point pt)
    {
        foreach (var kvp in _currentDoc.StateLayout)
        {
            if (!_currentDoc.Dfa.States.Contains(kvp.Key)) continue;
            Point center = new(kvp.Value.X, kvp.Value.Y);
            if ((pt - center).Length <= StateRadius)
                return kvp.Key;
        }
        return null;
    }

    private void DfaCanvas_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        Point pos = e.GetPosition(DfaCanvas);
        string? clickedState = GetStateAtPoint(pos);

        if (RbAddState.IsChecked == true)
        {
            if (clickedState != null) return;
            int idx = 0;
            while (_currentDoc.Dfa.States.Contains($"q{idx}")) idx++;
            string newState = $"q{idx}";

            _currentDoc.Dfa.States.Add(newState);
            if (_currentDoc.Dfa.InitialState == null)
                _currentDoc.Dfa.InitialState = newState;

            _currentDoc.StateLayout[newState] = new StateVisualInfo(pos.X, pos.Y, "");
            _selectedState = newState;

            OnAutomatonEdited();
        }
        else if (RbAddTransition.IsChecked == true)
        {
            if (clickedState == null)
            {
                _transitionSourceState = null;
                TxtCanvasHint.Text = "Click pe starea sursă, apoi pe starea destinație.";
                RedrawCanvas();
                return;
            }

            if (_transitionSourceState == null)
            {
                _transitionSourceState = clickedState;
                TxtCanvasHint.Text = $"Sursă selectată: {_transitionSourceState}. Acum dă click pe starea destinație!";
                RedrawCanvas();
            }
            else
            {
                string from = _transitionSourceState;
                string to = clickedState;
                _transitionSourceState = null;

                string? inputSym = PromptForSymbols(from, to);
                if (!string.IsNullOrWhiteSpace(inputSym))
                {
                    var symbols = inputSym.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                    foreach (var sym in symbols)
                    {
                        var res = _currentDoc.Dfa.AddTransition(from, sym, to);
                        if (!res.Success)
                        {
                            MessageBox.Show(res.ErrorMessage, "Regulă de Determinism (ED-04)", MessageBoxButton.OK, MessageBoxImage.Warning);
                        }
                    }
                    OnAutomatonEdited();
                }
                else
                {
                    RedrawCanvas();
                }
            }
        }
        else // Modul Mută / Selectează
        {
            _selectedState = clickedState;
            UpdateSelectedStatePanel();

            if (clickedState != null)
            {
                _draggingState = clickedState;
                var info = _currentDoc.StateLayout[clickedState];
                _dragOffset = new Point(pos.X - info.X, pos.Y - info.Y);
                DfaCanvas.CaptureMouse();
            }
            RedrawCanvas();
        }
    }

    private void DfaCanvas_MouseMove(object sender, MouseEventArgs e)
    {
        if (_draggingState != null && e.LeftButton == MouseButtonState.Pressed)
        {
            Point pos = e.GetPosition(DfaCanvas);
            double newX = Math.Clamp(pos.X - _dragOffset.X, StateRadius + 10, DfaCanvas.Width - StateRadius - 10);
            double newY = Math.Clamp(pos.Y - _dragOffset.Y, StateRadius + 10, DfaCanvas.Height - StateRadius - 10);

            var oldInfo = _currentDoc.StateLayout[_draggingState];
            _currentDoc.StateLayout[_draggingState] = oldInfo with { X = newX, Y = newY };
            RedrawCanvas();
        }
    }

    private void DfaCanvas_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (_draggingState != null)
        {
            _draggingState = null;
            DfaCanvas.ReleaseMouseCapture();
        }
    }

    private string? PromptForSymbols(string from, string to)
    {
        var win = new Window
        {
            Title = "Adaugă Tranziție",
            Width = 360,
            Height = 175,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Owner = this,
            ResizeMode = ResizeMode.NoResize,
            Background = new SolidColorBrush(Color.FromRgb(247, 247, 245))
        };

        var stack = new StackPanel { Margin = new Thickness(16) };
        stack.Children.Add(new TextBlock
        {
            Text = $"Tranziție din «{from}» către «{to}».\nIntrodu simbolul (sau mai multe separate prin virgulă, ex: a,b):",
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 0, 0, 8)
        });

        var txt = new TextBox { FontSize = 14, Padding = new Thickness(4), Text = _currentDoc.Dfa.Alphabet.FirstOrDefault() ?? "a" };
        stack.Children.Add(txt);

        var btn = new Button { Content = "Adaugă", IsDefault = true, Margin = new Thickness(0, 12, 0, 0), Padding = new Thickness(12, 4, 12, 4), HorizontalAlignment = HorizontalAlignment.Right };
        bool ok = false;
        btn.Click += (_, _) => { ok = true; win.Close(); };
        stack.Children.Add(btn);

        win.Content = stack;
        txt.Focus();
        txt.SelectAll();
        win.ShowDialog();

        return ok ? txt.Text : null;
    }

    private void UpdateSelectedStatePanel()
    {
        if (_selectedState == null || !_currentDoc.Dfa.States.Contains(_selectedState))
        {
            TxtSelectedState.Text = "Nicio stare selectată";
            ChkInitial.IsEnabled = ChkFinal.IsEnabled = TxtStateDesc.IsEnabled = BtnDeleteState.IsEnabled = false;
            ChkInitial.IsChecked = ChkFinal.IsChecked = false;
            TxtStateDesc.Text = "";
            return;
        }

        TxtSelectedState.Text = $"Starea: {_selectedState}";
        ChkInitial.IsEnabled = ChkFinal.IsEnabled = TxtStateDesc.IsEnabled = BtnDeleteState.IsEnabled = true;
        ChkInitial.IsChecked = _currentDoc.Dfa.InitialState == _selectedState;
        ChkFinal.IsChecked = _currentDoc.Dfa.FinalStates.Contains(_selectedState);
        TxtStateDesc.Text = _currentDoc.StateLayout.TryGetValue(_selectedState, out var info) ? info.Description : "";
    }

    private void ChkInitial_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedState == null) return;
        _currentDoc.Dfa.InitialState = ChkInitial.IsChecked == true ? _selectedState : null;
        OnAutomatonEdited();
    }

    private void ChkFinal_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedState == null) return;
        if (ChkFinal.IsChecked == true) _currentDoc.Dfa.FinalStates.Add(_selectedState);
        else _currentDoc.Dfa.FinalStates.Remove(_selectedState);
        OnAutomatonEdited();
    }

    private void TxtStateDesc_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_selectedState == null || !TxtStateDesc.IsEnabled) return;
        if (_currentDoc.StateLayout.TryGetValue(_selectedState, out var info))
        {
            _currentDoc.StateLayout[_selectedState] = info with { Description = TxtStateDesc.Text };
            RedrawCanvas();
        }
    }

    private void BtnDeleteState_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedState == null) return;
        _currentDoc.Dfa.States.Remove(_selectedState);
        _currentDoc.Dfa.FinalStates.Remove(_selectedState);
        if (_currentDoc.Dfa.InitialState == _selectedState)
            _currentDoc.Dfa.InitialState = _currentDoc.Dfa.States.FirstOrDefault();

        _currentDoc.Dfa.Transitions.RemoveAll(t => t.FromState == _selectedState || t.ToState == _selectedState);
        _currentDoc.StateLayout.Remove(_selectedState);
        _selectedState = null;

        OnAutomatonEdited();
    }

    private void BtnSetAlphabet_Click(object sender, RoutedEventArgs e)
    {
        var symbols = TxtAlphabet.Text.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        _currentDoc.Dfa.Alphabet = new HashSet<string>(symbols);
        OnAutomatonEdited();
    }

    private void BtnTrapState_Click(object sender, RoutedEventArgs e)
    {
        _currentDoc.Dfa.CompleteWithTrapState("q_cap");
        if (_currentDoc.Dfa.States.Contains("q_cap") && !_currentDoc.StateLayout.ContainsKey("q_cap"))
        {
            _currentDoc.StateLayout["q_cap"] = new StateVisualInfo(350, 320, "Stare-capcană (blocaj)");
        }
        OnAutomatonEdited();
    }

    // Cerința SI-07: Editarea automatului resetează simularea cu mesaj informativ
    private void OnAutomatonEdited()
    {
        UpdateSelectedStatePanel();
        UpdateDeltaTable();
        RunBatchTests();
        StartNewSimulation(true);
    }

    // =========================================================================
    // 4. TABELUL DE TRANZIȚII δ (ED-06) ȘI TESTAREA ÎN LOT (SI-08)
    // =========================================================================
    private void UpdateDeltaTable()
    {
        var dt = new DataTable();
        dt.Columns.Add("Stare");
        foreach (var sym in _currentDoc.Dfa.Alphabet)
        {
            dt.Columns.Add(sym);
        }

        foreach (var state in _currentDoc.Dfa.States)
        {
            var row = dt.NewRow();
            string prefix = "";
            if (_currentDoc.Dfa.InitialState == state) prefix += "→ ";
            if (_currentDoc.Dfa.FinalStates.Contains(state)) prefix += "* ";
            row["Stare"] = prefix + state;

            foreach (var sym in _currentDoc.Dfa.Alphabet)
            {
                row[sym] = _currentDoc.Dfa.GetNextState(state, sym) ?? "—";
            }
            dt.Rows.Add(row);
        }

        GridDelta.ItemsSource = dt.DefaultView;
    }

    private void BtnRunBatch_Click(object sender, RoutedEventArgs e) => RunBatchTests();

    private void RunBatchTests()
    {
        LstBatchTests.Items.Clear();
        if (_currentDoc.TestSuite.Count == 0)
        {
            TxtBatchSummary.Text = "0 teste";
            return;
        }

        int passed = 0;
        foreach (var tc in _currentDoc.TestSuite)
        {
            var run = new DfaRun(_currentDoc.Dfa, tc.Word);
            bool ok = run.IsAccepted == tc.ExpectedAccepted;
            if (ok) passed++;

            string dispWord = string.IsNullOrEmpty(tc.Word) ? "ε" : tc.Word;
            string expectedStr = tc.ExpectedAccepted ? "ACCEPTAT" : "RESPINS";
            string mark = ok ? "✓" : "✗";

            LstBatchTests.Items.Add($"{mark}  {dispWord,-12} (Așteptat: {expectedStr})");
        }

        TxtBatchSummary.Text = $"{passed} din {_currentDoc.TestSuite.Count} corecte";
    }

    private void LstBatchTests_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        int idx = LstBatchTests.SelectedIndex;
        if (idx >= 0 && idx < _currentDoc.TestSuite.Count)
        {
            TxtInputWord.Text = _currentDoc.TestSuite[idx].Word;
            StartNewSimulation(false);
        }
    }

    // =========================================================================
    // 5. SIMULAREA PAS CU PAS, BANDA DE INTRARE ȘI VERDICTUL (SI-01 .. SI-06)
    // =========================================================================
    private void StartNewSimulation(bool showResetNotice)
    {
        _playTimer.Stop();
        BtnPlayPause.Content = "▶ Play Automat";

        var issues = DfaValidator.Validate(_currentDoc.Dfa);
        var firstError = issues.FirstOrDefault(i => i.Severity == ValidationSeverity.Error);
        if (firstError != null)
        {
            _currentRun = null;
            GridExecution.ItemsSource = null;
            TapePanel.Children.Clear();
            TxtVerdict.Text = firstError.Message;
            BorderVerdict.Background = new SolidColorBrush(Color.FromRgb(253, 236, 234));
            TxtVerdict.Foreground = new SolidColorBrush(Color.FromRgb(179, 38, 30));
            RedrawCanvas();
            return;
        }

        _currentRun = new DfaRun(_currentDoc.Dfa, TxtInputWord.Text);
        GridExecution.ItemsSource = _currentRun.Steps;
        UpdateSimulationUI(showResetNotice);
    }

    private void UpdateSimulationUI(bool wasEditedReset = false)
    {
        if (_currentRun == null) return;

        GridExecution.SelectedIndex = _currentRun.CurrentStepIndex;
        GridExecution.ScrollIntoView(GridExecution.SelectedItem);

        // Actualizăm Banda de intrare (SI-02)
        TapePanel.Children.Clear();
        string word = _currentRun.InputWord;
        if (word.Length == 0)
        {
            TapePanel.Children.Add(CreateTapeCell("ε", true, false));
        }
        else
        {
            for (int i = 0; i < word.Length; i++)
            {
                bool isCurrent = i == _currentRun.CurrentStepIndex - 1;
                bool isAlreadyRead = i < _currentRun.CurrentStepIndex - 1;
                TapePanel.Children.Add(CreateTapeCell(word[i].ToString(), isCurrent, isAlreadyRead));
            }
        }

        // Actualizăm Verdictul (SI-06)
        if (wasEditedReset)
        {
            TxtVerdict.Text = "Simulare resetată (automatul a fost modificat).";
            BorderVerdict.Background = new SolidColorBrush(Color.FromRgb(232, 238, 252));
            TxtVerdict.Foreground = new SolidColorBrush(Color.FromRgb(47, 91, 211));
        }
        else if (_currentRun.IsAtEnd)
        {
            TxtVerdict.Text = _currentRun.VerdictMessage;
            if (_currentRun.IsAccepted)
            {
                BorderVerdict.Background = new SolidColorBrush(Color.FromRgb(227, 244, 234));
                TxtVerdict.Foreground = new SolidColorBrush(Color.FromRgb(31, 122, 76));
            }
            else
            {
                BorderVerdict.Background = new SolidColorBrush(Color.FromRgb(253, 236, 234));
                TxtVerdict.Foreground = new SolidColorBrush(Color.FromRgb(179, 38, 30));
            }
        }
        else
        {
            string st = _currentRun.CurrentStep.CurrentState ?? "blocaj";
            TxtVerdict.Text = $"În curs (Pasul {_currentRun.CurrentStepIndex}/{_currentRun.Steps.Count - 1}) · Stare curentă: {st}";
            BorderVerdict.Background = new SolidColorBrush(Color.FromRgb(232, 238, 252));
            TxtVerdict.Foreground = new SolidColorBrush(Color.FromRgb(47, 91, 211));
        }

        RedrawCanvas();
    }

    private UIElement CreateTapeCell(string symbol, bool isCurrent, bool isRead)
    {
        return new Border
        {
            Width = 34,
            Height = 36,
            Margin = new Thickness(2, 0, 2, 0),
            CornerRadius = new CornerRadius(5),
            Background = isCurrent ? new SolidColorBrush(Color.FromRgb(253, 241, 220))
                       : isRead ? new SolidColorBrush(Color.FromRgb(235, 237, 240))
                       : Brushes.White,
            BorderBrush = isCurrent ? new SolidColorBrush(Color.FromRgb(154, 91, 0)) : new SolidColorBrush(Color.FromRgb(200, 205, 212)),
            BorderThickness = new Thickness(isCurrent ? 2.5 : 1),
            Child = new TextBlock
            {
                Text = symbol,
                FontFamily = new FontFamily("Consolas"),
                FontSize = 15,
                FontWeight = FontWeights.Bold,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Foreground = isRead ? new SolidColorBrush(Color.FromRgb(150, 155, 165)) : new SolidColorBrush(Color.FromRgb(29, 31, 35))
            }
        };
    }

    private void BtnResetSim_Click(object sender, RoutedEventArgs e) => StartNewSimulation(false);

    private void BtnStepForward_Click(object sender, RoutedEventArgs e)
    {
        if (_currentRun == null) StartNewSimulation(false);
        _currentRun?.StepForward();
        UpdateSimulationUI();
    }

    private void BtnStepBack_Click(object sender, RoutedEventArgs e)
    {
        _currentRun?.StepBack();
        UpdateSimulationUI();
    }

    private void BtnRunToEnd_Click(object sender, RoutedEventArgs e)
    {
        if (_currentRun == null) StartNewSimulation(false);
        _currentRun?.RunToEnd();
        UpdateSimulationUI();
    }

    private void BtnPlayPause_Click(object sender, RoutedEventArgs e)
    {
        if (_currentRun == null) StartNewSimulation(false);
        if (_playTimer.IsEnabled)
        {
            _playTimer.Stop();
            BtnPlayPause.Content = "▶ Play Automat";
        }
        else
        {
            if (_currentRun != null && _currentRun.IsAtEnd)
                _currentRun.Reset();
            _playTimer.Start();
            BtnPlayPause.Content = "⏸ Pauză";
        }
    }

    private void PlayTimer_Tick(object? sender, EventArgs e)
    {
        if (_currentRun == null || !_currentRun.StepForward())
        {
            _playTimer.Stop();
            BtnPlayPause.Content = "▶ Play Automat";
        }
        UpdateSimulationUI();
    }

    private void SliderSpeed_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        _playTimer.Interval = TimeSpan.FromMilliseconds(1700 - SliderSpeed.Value);
    }

    // =========================================================================
    // 6. TAB 2: ECRANUL DEDICAT PENTRU NUMERE DE ÎNMATRICULARE (A1)
    // =========================================================================
    private void TxtLicensePlate_TextChanged(object sender, TextChangedEventArgs e)
    {
        ValidateLicensePlateLive();
    }

    private void ValidateLicensePlateLive()
    {
        if (TxtPlateVerdict == null) return;

        string input = TxtLicensePlate.Text;
        var run = new DfaRun(_licensePlateDoc.Dfa, input);
        run.RunToEnd();

        string? lastState = run.CurrentStep.CurrentState;

        if (run.IsAccepted)
        {
            PlateStatusBorder.Background = new SolidColorBrush(Color.FromRgb(227, 244, 234));
            PlateStatusBorder.BorderBrush = new SolidColorBrush(Color.FromRgb(31, 122, 76));
            TxtPlateVerdict.Foreground = new SolidColorBrush(Color.FromRgb(31, 122, 76));
            TxtPlateVerdict.Text = "✓ NUMĂR DE ÎNMATRICULARE VALID";
            TxtPlateStateHint.Text = $"Stare finală atinsă: {lastState} — {_licensePlateDoc.StateLayout[lastState!].Description}";
        }
        else if (lastState != null && input.Length < 9)
        {
            // Încă tastează (nu s-a blocat, dar nu a ajuns încă în starea finală)
            PlateStatusBorder.Background = new SolidColorBrush(Color.FromRgb(253, 241, 220));
            PlateStatusBorder.BorderBrush = new SolidColorBrush(Color.FromRgb(154, 91, 0));
            TxtPlateVerdict.Foreground = new SolidColorBrush(Color.FromRgb(154, 91, 0));
            string hint = _licensePlateDoc.StateLayout.TryGetValue(lastState, out var info) ? info.Description : "";
            TxtPlateVerdict.Text = "⏳ FORMAT INCOMPLET (continuă să tastezi...)";
            TxtPlateStateHint.Text = $"Stare curentă ({lastState}): {hint}";
        }
        else
        {
            PlateStatusBorder.Background = new SolidColorBrush(Color.FromRgb(253, 236, 234));
            PlateStatusBorder.BorderBrush = new SolidColorBrush(Color.FromRgb(179, 38, 30));
            TxtPlateVerdict.Foreground = new SolidColorBrush(Color.FromRgb(179, 38, 30));
            TxtPlateVerdict.Text = "✗ FORMAT INVALID";
            TxtPlateStateHint.Text = run.VerdictMessage;
        }
    }

    private void BtnQuickPlate_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string plate)
        {
            TxtLicensePlate.Text = plate;
        }
    }

    private void BtnOpenPlateInSim_Click(object sender, RoutedEventArgs e)
    {
        string currentPlate = TxtLicensePlate.Text;
        CmbExamples.SelectedIndex = 2; // Selectează A1 în simulator
        TxtInputWord.Text = currentPlate;
        StartNewSimulation(false);

        // Comută pe primul Tab (Simulatorul)
        if (Content is Grid g && g.Children[0] is TabControl tc)
        {
            tc.SelectedIndex = 0;
        }
    }
}