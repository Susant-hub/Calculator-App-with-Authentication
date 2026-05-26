import { useState } from 'react';
import { Link, useNavigate } from 'react-router-dom';
import axios from 'axios';
import styles from './Register.module.css';
import CalculatorBoldDuotoneIcon from '@iconify-react/solar/calculator-bold-duotone';

const API_BASE = import.meta.env.VITE_API_URL || 'http://localhost:5275';


export default function Register({ onLogin }) {
  const navigate = useNavigate();
  const [form, setForm] = useState({ email: '', username: '', password: '', confirm: '' });
  const [error, setError] = useState('');
  const [loading, setLoading] = useState(false);

  const handleChange = (e) => {
    setForm({ ...form, [e.target.name]: e.target.value });
    setError('');
  };

  const handleSubmit = async (e) => {
    e.preventDefault();
    setLoading(true);
    setError('');
    try {
      const res = await axios.post(`${API_BASE}/api/auth/register`, {
        email: form.email.trim(),
        username: form.username.trim(),
        password: form.password,
      });
      localStorage.setItem('token', res.data.token);
      localStorage.setItem('username', res.data.username);
      onLogin();          
      navigate('/dashboard');
    } catch (err) {
      setError(err.response?.data || 'Registration failed. Please try again.');
    } finally {
      setLoading(false);
    }
  };

  return (
    <div className={styles.page}>
      <div className={styles.card}>
        <div className={styles.logo}>
          <CalculatorBoldDuotoneIcon height="2em" color='#7c5cfc'/>
          <span className={styles.logoText}>SusCalc</span>
        </div>

        <h1 className={styles.title}>Create account</h1>
        <p className={styles.subtitle}>Start calculating in seconds</p>

        {error && <div className={styles.error}>{error}</div>}

        <form onSubmit={handleSubmit} className={styles.form}>
          
            <div className={styles.field}>
              <label className={styles.label}>Username</label>
              <input
                className={styles.input}
                type="text"
                name="username"
                placeholder="susant"
                value={form.username}
                onChange={handleChange}
                required
              />
            </div>
            <div className={styles.field}>
              <label className={styles.label}>Email</label>
              <input
                className={styles.input}
                type="email"
                name="email"
                placeholder="ss@123.com"
                value={form.email}
                onChange={handleChange}
                required
              />
            </div>
          

          <div className={styles.field}>
            <label className={styles.label}>Password</label>
            <input
              className={styles.input}
              type="password"
              name="password"
              placeholder="At least 6 characters"
              value={form.password}
              onChange={handleChange}
              required
            />
          </div>


          <button className={styles.btn} type="submit" disabled={loading}>
            {loading ? <span className={styles.spinner}></span> : 'Create Account'}
          </button>
        </form>

        <p className={styles.footer}>
          Already have an account?{' '}
          <Link to="/login" className={styles.link}>Sign in</Link>
        </p>
      </div>
    </div>
  );
}