using LiveCDKRedeem.Bean;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.System;

namespace LiveCDKRedeem.ViewModel
{
    public class AccountViewModel : INotifyPropertyChanged
    {
        private ObservableCollection<AccountData> _accounts;

        public ObservableCollection<AccountData> Accounts
        {
            get { return _accounts; }
            set
            {
                //if (_accounts != value)
                //{
                    _accounts = value;
                    OnPropertyChanged("Accounts");
               // }
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
            Debug.WriteLine($"AccountViewModel剩余Item数量 ---> {_accounts.Count}");
        }

        public void Add(AccountData accountData)
        {
            if (this._accounts == null)
            {
                this._accounts = new ObservableCollection<AccountData>();
            }
            this._accounts.Add(accountData);
            OnPropertyChanged("Accounts");
            
        }
        public void Remove(AccountData accountData)
        {
            if (this._accounts == null)
            {
                this._accounts = new ObservableCollection<AccountData>();
            }
            this._accounts.Remove(accountData);
            OnPropertyChanged("Accounts");
        }
    }
}

