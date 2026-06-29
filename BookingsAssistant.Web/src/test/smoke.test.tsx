import { describe, it, expect } from 'vitest';
import { render, screen } from '@testing-library/react';

// Proves the test harness is wired: Vitest runs, RTL renders into jsdom,
// and the jest-dom matchers (toBeInTheDocument) are available.
describe('test harness', () => {
  it('renders a component and jest-dom matchers work', () => {
    render(<div>harness ok</div>);
    expect(screen.getByText('harness ok')).toBeInTheDocument();
  });
});
