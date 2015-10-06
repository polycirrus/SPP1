using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.IO;

namespace SPP1.Generator
{
    public partial class Form1 : Form
    {
        public Form1()
        {
            InitializeComponent();
            random = new Random();
        }

        private Random random;

        private void generateButton_Click(object sender, EventArgs e)
        {
            var writer = new StreamWriter(@"C:\dump\a1.txt", false, Encoding.Unicode);

            String line;
            for (int i = 1; i <= random.Next(9000000, 11000000); i++)
            {
                line = RandomWord();
                for (int j = 1; j <= random.Next(2, 6); j++)
                    line += " " + RandomWord();
                writer.WriteLine(line);
            }

            writer.Flush();
            writer.Close();

            MessageBox.Show("done");
        }

        private String RandomWord()
        {
            const String chars = "абвгдеёжзийклмнопрстуфхцчшщъыьэюяАБВГДЕЁЖЗИЙКЛМНОПРСТУФХЦЧШЩЪЫЬЭЮЯABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789!@#$%^&*()-=_+";

            var wordLength = random.Next(3, 10);
            var wordChars = new Char[wordLength];
            for (int i = 0; i < wordLength; i++)
                wordChars[i] = chars[random.Next(chars.Length)];

            return new String(wordChars);
        }
    }
}
