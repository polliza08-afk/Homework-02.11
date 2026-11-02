using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace TestingSystem
{
    //Моделі даних та сервіс вбудованя в один файл для зручності
    public enum GradingSystem
    {
        Points12,
        Points100,
        Points5
    }

    public class AnswerOption
    {
        public string Text { get; set; }
        public bool IsCorrect { get; set; }
    }

    public class Question
    {
        public string Text { get; set; }
        public List<AnswerOption> Options { get; set; } = new List<AnswerOption>();
    }

    public class TestParameters
    {
        public TimeSpan Duration { get; set; }
        public GradingSystem Grading { get; set; }
    }

    public class Test
    {
        public string Title { get; set; }
        public TestParameters Parameters { get; set; } = new TestParameters();
        public List<Question> Questions { get; set; } = new List<Question>();
    }

    public class UserInfo
    {
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public string GroupOrEmail { get; set; }
    }

    public class TestResult
    {
        public UserInfo User { get; set; }
        public Test Test { get; set; }
        public int CorrectAnswersCount { get; set; }
        public int TotalQuestionsCount { get; set; }
        public double FinalScore { get; set; }
        public TimeSpan ElapsedTime { get; set; }
    }

    public class TestingManager
    {
        public Test CurrentTest { get; private set; }
        public UserInfo CurrentUser { get; private set; }
        private DateTime _startTime;

        public void StartTest(Test test, UserInfo user)
        {
            CurrentTest = test;
            CurrentUser = user;
            _startTime = DateTime.Now;
        }

        public TestResult CalculateResult(Dictionary<int, List<int>> userAnswers)
        {
            int correctCount = 0;
            int totalQuestions = CurrentTest.Questions.Count;

            for (int i = 0; i < totalQuestions; i++)
            {
                var question = CurrentTest.Questions[i];
                var selectedIndices = userAnswers.ContainsKey(i) ? userAnswers[i] : new List<int>();

                if (IsAnswerCorrect(question, selectedIndices))
                {
                    correctCount++;
                }
            }

            var elapsedTime = DateTime.Now - _startTime;
            double finalScore = ConvertScore(correctCount, totalQuestions, CurrentTest.Parameters.Grading);

            return new TestResult
            {
                User = CurrentUser,
                Test = CurrentTest,
                CorrectAnswersCount = correctCount,
                TotalQuestionsCount = totalQuestions,
                FinalScore = finalScore,
                ElapsedTime = elapsedTime
            };
        }

        private bool IsAnswerCorrect(Question question, List<int> selectedIndices)
        {
            for (int j = 0; j < question.Options.Count; j++)
            {
                bool isSelected = selectedIndices.Contains(j);
                bool shouldBeSelected = question.Options[j].IsCorrect;

                if (isSelected != shouldBeSelected) return false;
            }
            return true;
        }

        private double ConvertScore(int correct, int total, GradingSystem grading)
        {
            if (total == 0) return 0;
            double percentage = (double)correct / total;

            return grading switch
            {
                GradingSystem.Points12 => Math.Round(percentage * 12, 1),
                GradingSystem.Points100 => Math.Round(percentage * 100, 1),
                GradingSystem.Points5 => Math.Round(percentage * 4 + 1, 1),
                _ => percentage * 100
            };
        }
    }

    //Головне вікно
    public partial class MainForm : Window
    {
        private TestingManager _manager;
        private Test _currentTest;
        private int _currentQuestionIndex = 0;
        private Dictionary<int, List<int>> _userAnswers;
        private DispatcherTimer _timer;
        private TimeSpan _remainingTime;

        public MainForm()
        {
            InitializeComponent();
            _manager = new TestingManager();
            _userAnswers = new Dictionary<int, List<int>>();
            _currentTest = CreateOopTest();
        }

        private void StartTest_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(txtFirstName.Text) || string.IsNullOrWhiteSpace(txtLastName.Text))
            {
                MessageBox.Show("Будь ласка, введіть ім'я та прізвище!", "Помилка", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var userInfo = new UserInfo
            {
                FirstName = txtFirstName.Text,
                LastName = txtLastName.Text,
                GroupOrEmail = txtGroup.Text
            };

            _manager.StartTest(_currentTest, userInfo);
            _currentQuestionIndex = 0;
            _userAnswers.Clear();

            _remainingTime = _currentTest.Parameters.Duration;
            _timer = new DispatcherTimer();
            _timer.Interval = TimeSpan.FromSeconds(1);
            _timer.Tick += Timer_Tick;
            _timer.Start();

            pnlRegistration.Visibility = Visibility.Collapsed;
            pnlTest.Visibility = Visibility.Visible;

            LoadQuestion();
        }

        private void Timer_Tick(object sender, EventArgs e)
        {
            if (_remainingTime.TotalSeconds > 0)
            {
                _remainingTime = _remainingTime.Subtract(TimeSpan.FromSeconds(1));
                lblTimer.Text = $"Час: {_remainingTime:mm\\:ss}";
            }
            else
            {
                _timer.Stop();
                MessageBox.Show("Час вийшов!", "Увага", MessageBoxButton.OK, MessageBoxImage.Information);
                FinishTest();
            }
        }

        private void LoadQuestion()
        {
            var question = _currentTest.Questions[_currentQuestionIndex];
            lblQuestionNumber.Text = $"Питання {_currentQuestionIndex + 1} з {_currentTest.Questions.Count}";
            lblQuestionText.Text = question.Text;

            pnlOptions.Children.Clear();

            for (int i = 0; i < question.Options.Count; i++)
            {
                var option = question.Options[i];
                var radioButton = new RadioButton
                {
                    Content = option.Text,
                    Tag = i,
                    FontSize = 14,
                    Margin = new Thickness(0, 5, 0, 5)
                };

                if (_userAnswers.ContainsKey(_currentQuestionIndex) && _userAnswers[_currentQuestionIndex].Contains(i))
                {
                    radioButton.IsChecked = true;
                }

                pnlOptions.Children.Add(radioButton);
            }

            btnNext.Content = (_currentQuestionIndex == _currentTest.Questions.Count - 1) ? "Завершити тест" : "Наступне питання";
        }

        private void NextQuestion_Click(object sender, RoutedEventArgs e)
        {
            var selectedIndices = new List<int>();
            foreach (var child in pnlOptions.Children)
            {
                if (child is RadioButton rb && rb.IsChecked == true)
                {
                    selectedIndices.Add((int)rb.Tag);
                }
            }

            _userAnswers[_currentQuestionIndex] = selectedIndices;
            _currentQuestionIndex++;

            if (_currentQuestionIndex < _currentTest.Questions.Count)
            {
                LoadQuestion();
            }
            else
            {
                _timer.Stop();
                FinishTest();
            }
        }

        private void FinishTest()
        {
            var result = _manager.CalculateResult(_userAnswers);

            pnlTest.Visibility = Visibility.Collapsed;
            pnlResult.Visibility = Visibility.Visible;

            lblResultUser.Text = $"Користувач: {result.User.FirstName} {result.User.LastName} ({result.User.GroupOrEmail})";
            lblResultScore.Text = $"Оцінка: {result.FinalScore} (Система: 12-бальна, Правильних: {result.CorrectAnswersCount}/{result.TotalQuestionsCount})";
            lblResultTime.Text = $"Витрачений час: {result.ElapsedTime:mm\\:ss}";
        }

        private void Restart_Click(object sender, RoutedEventArgs e)
        {
            txtFirstName.Clear();
            txtLastName.Clear();
            txtGroup.Clear();

            pnlResult.Visibility = Visibility.Collapsed;
            pnlRegistration.Visibility = Visibility.Visible;
        }

        private Test CreateOopTest()
        {
            return new Test
            {
                Title = "Основи ООП",
                Parameters = new TestParameters
                {
                    Duration = TimeSpan.FromMinutes(10),
                    Grading = GradingSystem.Points12
                },
                Questions = new List<Question>
                {
                    new Question
                    {
                        Text = "Які є основні принципи ООП?",
                        Options = new List<AnswerOption>
                        {
                            new AnswerOption { Text = "Інкапсуляція, Спадкування, Поліморфізм", IsCorrect = true },
                            new AnswerOption { Text = "Лінійність, Послідовність, Вкладеність", IsCorrect = false },
                            new AnswerOption { Text = "Компіляція, Інтерпретація, Абстракція", IsCorrect = false }
                        }
                    },
                    new Question
                    {
                        Text = "Що таке інкапсуляція?",
                        Options = new List<AnswerOption>
                        {
                            new AnswerOption { Text = "Здатність об'єкта приховувати внутрішній стан та реалізацію", IsCorrect = true },
                            new AnswerOption { Text = "Створення копії класу", IsCorrect = false },
                            new AnswerOption { Text = "Перевизначення методів базового класу", IsCorrect = false }
                        }
                    }
                }
            };
        }
    }
}