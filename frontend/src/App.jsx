import { useState, useEffect } from 'react';
import './App.css';

// Component to show the live countdown
function CountdownTimer({ expiresAt }) {
  const [timeLeft, setTimeLeft] = useState(0);

  useEffect(() => {
    const calculateTimeLeft = () => {
      const difference = new Date(expiresAt).getTime() - new Date().getTime();
      setTimeLeft(difference > 0 ? Math.floor(difference / 1000) : 0);
    };

    calculateTimeLeft();
    const timer = setInterval(calculateTimeLeft, 1000);
    return () => clearInterval(timer);
  }, [expiresAt]);

  if (timeLeft <= 0) return <span style={{ color: 'red', fontWeight: 'bold' }}>EXPIRED</span>;

  const minutes = Math.floor(timeLeft / 60);
  const seconds = timeLeft % 60;
  return <span style={{ color: '#ffc107', fontWeight: 'bold' }}>{minutes}m {seconds}s left</span>;
}

function App() {
  const [role, setRole] = useState('admin'); 
  const [studentId] = useState('20152691234567890');
  const [classes, setClasses] = useState([]);
  const [newClassName, setNewClassName] = useState('');
  const [newClassSeats, setNewClassSeats] = useState(120);
  const [myHolds, setMyHolds] = useState([]);

  const fetchClasses = async () => {
    try {
      const response = await fetch('http://localhost:8080/classes');
      if (response.ok) setClasses(await response.json());
    } catch (error) { console.error("Error fetching classes:", error); }
  };

  const fetchMyHolds = async () => {
    try {
      const response = await fetch('http://localhost:8080/me/holds', {
        headers: { 'X-Student-Id': studentId }
      });
      if (response.ok) setMyHolds(await response.json());
    } catch (error) { console.error("Error fetching holds:", error); }
  };

  useEffect(() => {
    fetchClasses();
    fetchMyHolds();
    const interval = setInterval(() => { fetchClasses(); fetchMyHolds(); }, 5000);
    return () => clearInterval(interval);
  }, []);

  const handleCreateClass = async (e) => {
    e.preventDefault();
    try {
      await fetch('http://localhost:8080/classes', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ name: newClassName, totalSeats: parseInt(newClassSeats) })
      });
      setNewClassName('');
      fetchClasses();
    } catch (error) { alert("Failed to create class"); }
  };

  const handleHold = async (classId) => {
    try {
      const response = await fetch(`http://localhost:8080/classes/${classId}/holds`, {
        method: 'POST',
        headers: { 'X-Student-Id': studentId }
      });
      if (response.ok) { fetchClasses(); fetchMyHolds(); } 
      else { const data = await response.json(); alert(data.message || "Failed to hold seat"); }
    } catch (error) { alert("Error holding seat"); }
  };

  const handleConfirm = async (holdId) => {
    try {
      const response = await fetch(`http://localhost:8080/holds/${holdId}/confirm`, {
        method: 'POST',
        headers: { 'X-Student-Id': studentId }
      });
      if (response.ok) { alert("Payment Successful! Seat Confirmed."); fetchClasses(); fetchMyHolds(); } 
      else { const data = await response.json(); alert(data.message || "Failed to confirm"); }
    } catch (error) { alert("Error confirming payment"); }
  };

  const handleCancel = async (holdId) => {
    try {
      const response = await fetch(`http://localhost:8080/holds/${holdId}`, {
        method: 'DELETE',
        headers: { 'X-Student-Id': studentId }
      });
      if (response.ok) { fetchClasses(); fetchMyHolds(); }
    } catch (error) { alert("Error cancelling hold"); }
  };

  return (
    <div className="container">
      <header>
        <h1>School Admission System</h1>
        <div className="role-switch">
          <button onClick={() => setRole('admin')} className={role === 'admin' ? 'active' : ''}>Admin</button>
          <button onClick={() => setRole('student')} className={role === 'student' ? 'active' : ''}>Student</button>
        </div>
      </header>

      {role === 'admin' && (
        <div className="admin-view">
          <h2>Create New Class</h2>
          <form onSubmit={handleCreateClass}>
            <input type="text" placeholder="Class Name (e.g., Class 6 - Morning Shift)" value={newClassName} onChange={(e) => setNewClassName(e.target.value)} required />
            <input type="number" placeholder="Total Seats" value={newClassSeats} onChange={(e) => setNewClassSeats(e.target.value)} required />
            <button type="submit">Create Class</button>
          </form>
        </div>
      )}

      {role === 'student' && (
        <div className="student-view">
          <h2>Student Dashboard</h2>
          <p>Your Student ID: <strong>{studentId}</strong></p>
          
          <h3>My Applications</h3>
          {myHolds.length === 0 ? (
            <p>You have no applications yet.</p>
          ) : (
            <div className="my-holds">
              {myHolds.map((hold) => (
                <div key={hold.holdId} className="hold-card">
                  <p><strong>{hold.className}</strong></p>
                  <p>Status: <span className={`status-${hold.status}`}>{hold.status}</span></p>
                  
                  {/* Show countdown if status is HELD */}
                  {hold.status === 'HELD' && (
                    <p>Time left: <CountdownTimer expiresAt={hold.expiresAt} /></p>
                  )}

                  {hold.status === 'HELD' && (
                    <div className="action-buttons">
                      <button onClick={() => handleConfirm(hold.holdId)} className="btn-confirm">Pay Fee (Confirm)</button>
                      <button onClick={() => handleCancel(hold.holdId)} className="btn-cancel">Cancel</button>
                    </div>
                  )}
                </div>
              ))}
            </div>
          )}
        </div>
      )}

      <div className="classes-list">
        <h2>Available Classes</h2>
        {classes.length === 0 ? (
          <p>No classes available yet.</p>
        ) : (
          <div className="grid">
            {classes.map((cls) => {
              const existingHold = myHolds.find(h => h.classId === cls.id && (h.status === 'HELD' || h.status === 'CONFIRMED'));
              return (
                <div key={cls.id} className="card">
                  <h3>{cls.name}</h3>
                  <p>Total Seats: <strong>{cls.totalSeats}</strong></p>
                  <p>Available Seats: <strong>{cls.availableSeats}</strong></p>
                  <p>Held Seats: {cls.heldSeats}</p>
                  <p>Confirmed Seats: {cls.confirmedSeats}</p>
                  {role === 'student' && (
                    <button disabled={cls.availableSeats <= 0 || existingHold} onClick={() => handleHold(cls.id)}>
                      {existingHold ? 'Already Applied' : (cls.availableSeats > 0 ? 'Apply (Hold Seat)' : 'No Seats Available')}
                    </button>
                  )}
                </div>
              );
            })}
          </div>
        )}
      </div>
    </div>
  );
}

export default App;