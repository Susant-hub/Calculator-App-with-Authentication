import { useState, useEffect, useCallback } from 'react';
import { useNavigate } from 'react-router-dom';
import axios from 'axios';
import { evaluate } from 'mathjs';
import styles from './Dashboard.module.css';
import CalculatorBoldDuotoneIcon from '@iconify-react/solar/calculator-bold-duotone';

const API_BASE = import.meta.env.VITE_API_URL || 'http://localhost:5275';

const authHeader = () => ({
  Authorization: `Bearer ${localStorage.getItem('token')}`
});

const BUTTONS = [
  { label: 'C',  type: 'clear' },
  { label: 'CE', type: 'clear' },
  { label: '⌫',  type: 'clear' },
  { label: '÷',  type: 'op'    },

  { label: '7',  type: 'digit' },
  { label: '8',  type: 'digit' },
  { label: '9',  type: 'digit' },
  { label: 'x',  type: 'op'    },

  { label: '4',  type: 'digit' },
  { label: '5',  type: 'digit' },
  { label: '6',  type: 'digit' },
  { label: '-',  type: 'op'    },

  { label: '1',  type: 'digit' },
  { label: '2',  type: 'digit' },
  { label: '3',  type: 'digit' },
  { label: '+',  type: 'op'    },

  { label: 'x²', type: 'fn'    },
  { label: '√',  type: 'fn'    },
  { label: '.',  type: 'digit' },
  { label: '=',  type: 'equals'},

  { label: '0',  type: 'digit', wide: true },
];

const QUOTES = [
  { text: "Numbers have life; they're not just symbols on paper.", author: 'Shakuntala Devi' },
  { text: 'Mathematics is the language in which God has written the universe.', author: 'Galileo Galilei' },
  { text: 'Pure mathematics is, in its way, the poetry of logical ideas.', author: 'Albert Einstein' },
];

const randomQuote = QUOTES[Math.floor(Math.random() * QUOTES.length)];

const toMathExpr = (display) =>
  display
    .replace(/÷/g, '/')
    .replace(/x/g, '*')
    .replace(/²/g, '^2')
    .replace(/√(\d+(\.\d+)?)/g, 'sqrt($1)');

export default function Dashboard({ onLogout }) {
  const navigate = useNavigate();
  const username = localStorage.getItem('username') || 'User';

  const [display, setDisplay]       = useState('');
  const [result, setResult]         = useState('');
  const [history, setHistory]       = useState([]);
  const [loadingClear, setLoadingClear] = useState(false);
  const [keyError, setKeyError]     = useState('');

  const fetchHistory = useCallback(async () => {
    try {
      const res = await axios.get(`${API_BASE}/api/calculations`, { headers: authHeader() });
      setHistory(res.data.slice().reverse());
    } catch (err) {
      console.error('Failed to fetch history', err);
    }
  }, []);

  useEffect(() => { fetchHistory(); }, [fetchHistory]);

  const handleButton = (label) => {
    if (label === null) return;
    if (label === 'C')  { setDisplay(''); setResult(''); return; }
    if (label === 'CE') { setDisplay(''); return; }
    if (label === '⌫')  { setDisplay(prev => prev.slice(0, -1)); return; }
    if (label === '=')  { handleEvaluate(); return; }
    if (label === 'x²') { setDisplay(prev => prev + '²'); return; }
    if (label === '√')  { setDisplay(prev => prev + '√'); return; }
    setDisplay(prev => prev + label);
  };

  const handleEvaluate = async () => {
    if (!display.trim()) return;
    try {
      const evalResult = evaluate(toMathExpr(display));
      if (!isFinite(evalResult)) throw new Error();

      const resultStr = parseFloat(evalResult.toFixed(8)).toString();
      setResult(resultStr);

      await axios.post(`${API_BASE}/api/calculations`,
        { expression: display, result: resultStr },
        { headers: authHeader() }
      );
      fetchHistory();
    } catch {
      setResult('Error');
    }
  };

  const clearHistory = async () => {
    setLoadingClear(true);
    try {
      await axios.delete(`${API_BASE}/api/calculations`, { headers: authHeader() });
      setHistory([]);
    } catch (err) {
      console.error('Failed to clear history', err);
    } finally {
      setLoadingClear(false);
    }
  };

  const handleLogout = () => {
    localStorage.removeItem('token');
    localStorage.removeItem('username');
    if (onLogout) onLogout();
    navigate('/login');
  };

  // Keyboard support
  useEffect(() => {
    const handleKeyDown = (e) => {
      const key = e.key;
      // Prevent default for keys that might cause browser actions
      const preventKeys = ['Enter', 'Backspace', 'Escape', 'Tab', 'Delete', 'Home', 'End', 'ArrowUp', 'ArrowDown', 'ArrowLeft', 'ArrowRight'];
      if (preventKeys.includes(key)) {
        e.preventDefault();
      }

      // Map keys to button labels
      let mapped = null;
      if (/^[0-9]$/.test(key)) mapped = key;
      else if (key === '+') mapped = '+';
      else if (key === '-') mapped = '-';
      else if (key === '*') mapped = 'x';
      else if (key === '/') mapped = '÷';
      else if (key === '.') mapped = '.';
      else if (key === 'Enter') mapped = '=';
      else if (key === 'Backspace') mapped = '⌫';
      else if (key === 'Escape') mapped = 'C';
      else if (key === 'Delete') mapped = 'CE';
      else if (key === '^' || key === '**') mapped = 'x²';
      else if (key === 's' || key === 'S') mapped = '√';   // 's' for sqrt
      else if (key === 'c' || key === 'C') mapped = 'C';
      else if (key === 'r') mapped = 'CE'; // 'r' for reset

      if (mapped) {
        handleButton(mapped);
        setKeyError('');
      } else {
        // Unsupported key
        setKeyError(`Cannot perform operation: "${key}"`);
        setTimeout(() => setKeyError(''), 2000);
      }
    };

    window.addEventListener('keydown', handleKeyDown);
    return () => window.removeEventListener('keydown', handleKeyDown);
  }, [display, result, handleButton]);

  return (
    <div className={styles.page}>

      <nav className={styles.navbar}>
        <div className={styles.logo}>
          <CalculatorBoldDuotoneIcon height="2em" color='#7c5cfc' />
          <span className={styles.logoText}>SusCalc</span>
        </div>
        <div className={styles.userInfo}>
          <img
            src={`https://ui-avatars.com/api/?name=${encodeURIComponent(username)}&background=random&color=fff&size=32`}
            alt="avatar"
            className={styles.avatar}
          />
          <button onClick={handleLogout} className={styles.logoutBtn}>Logout</button>
        </div>
      </nav>

      <div className={styles.hero}>
        <div className={styles.heroText}>
          <h1>Hello, {username} 👋</h1>
          <p>"{randomQuote.text}" — {randomQuote.author}</p>
        </div>
        <div className={styles.heroEmoji}>🧮</div>
      </div>

      {/* Keyboard error message */}
      {keyError && (
        <div style={{
          backgroundColor: '#ffebee',
          color: '#c62828',
          padding: '8px 16px',
          borderRadius: '8px',
          margin: '0 1.5rem 1rem 1.5rem',
          textAlign: 'center',
          fontSize: '0.9rem'
        }}>
          {keyError}
        </div>
      )}

      <div className={styles.mainContent}>

        <div className={styles.calculator}>
          <div className={styles.display}>
            <div className={styles.expression}>{display || '0'}</div>
            <div className={styles.result}>{result}</div>
          </div>
          <div className={styles.buttons}>
            {BUTTONS.map((btn, idx) => (
              <button
                key={idx}
                className={[
                  styles.btn,
                  styles[btn.type],
                  btn.wide ? styles.wide : ''
                ].filter(Boolean).join(' ')}
                onClick={() => handleButton(btn.label)}
              >
                {btn.label}
              </button>
            ))}
          </div>
        </div>

        <div className={styles.history}>
          <div className={styles.historyHeader}>
            <h3>History</h3>
            <button onClick={clearHistory} disabled={loadingClear} className={styles.clearBtn}>
              {loadingClear ? 'Clearing...' : 'Clear all'}
            </button>
          </div>
          <div className={styles.historyList}>
            {history.length === 0
              ? <div className={styles.emptyHistory}>No calculations yet.</div>
              : history.map((item, i) => (
                <div key={i} className={styles.historyItem}>
                  <span className={styles.historyExpr}>{item.expression}</span>
                  <span className={styles.historyResult}>{item.result}</span>
                </div>
              ))
            }
          </div>
        </div>

      </div>
    </div>
  );
}