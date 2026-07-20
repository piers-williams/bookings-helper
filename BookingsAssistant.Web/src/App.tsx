import { Routes, Route } from 'react-router-dom';
import Dashboard from './components/Dashboard';
import BookingDetail from './components/BookingDetail';
import BookingList from './components/BookingList';
import Triage from './components/Triage';

function App() {
  return (
    <div className="min-h-screen bg-gray-100">
      <Routes>
        <Route path="/" element={<Dashboard />} />
        <Route path="/bookings" element={<BookingList />} />
        <Route path="/bookings/:id" element={<BookingDetail />} />
        <Route path="/triage" element={<Triage />} />
      </Routes>
    </div>
  );
}

export default App;
