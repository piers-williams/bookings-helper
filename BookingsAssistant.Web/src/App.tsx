import { Routes, Route } from 'react-router-dom';
import Dashboard from './components/Dashboard';
import BookingDetail from './components/BookingDetail';

function App() {
  return (
    <div className="min-h-screen bg-gray-100">
      <Routes>
        <Route path="/" element={<Dashboard />} />
        <Route path="/bookings/:id" element={<BookingDetail />} />
      </Routes>
    </div>
  );
}

export default App;
