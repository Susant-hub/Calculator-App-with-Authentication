import { useNavigate } from 'react-router-dom';

function Dashboard() {
  const navigate = useNavigate();
  const username = localStorage.getItem('username');

  const handleLogout = () => {
    localStorage.removeItem('token');
    localStorage.removeItem('username');
    navigate('/login');
  };

  return (
    <div>
      <div>
        <h3>Hello, {username}!</h3>
        <button onClick={handleLogout}>Logout</button>
      </div>
      <div>
        <h2>Calculator</h2>
        <p>History </p>
      </div>
    </div>
  );
}

export default Dashboard;