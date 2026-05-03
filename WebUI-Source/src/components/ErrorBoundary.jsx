import React from 'react'

export class ErrorBoundary extends React.Component {
  constructor(props) {
    super(props)
    this.state = { hasError: false, error: null }
  }

  static getDerivedStateFromError(error) {
    return { hasError: true, error }
  }

  componentDidCatch(error, errorInfo) {
    console.error("ErrorBoundary caught an error", error, errorInfo)
  }

  render() {
    if (this.state.hasError) {
      return (
        <div style={{
          height: '100%',
          width: '100%',
          display: 'flex',
          flexDirection: 'column',
          alignItems: 'center',
          justifyContent: 'center',
          padding: 20,
          background: '#1A1A1A',
          color: '#FF3B30',
          textAlign: 'center',
          fontFamily: '-apple-system, sans-serif'
        }}>
          <h2 style={{ margin: '0 0 10px 0' }}>Interface Error</h2>
          <p style={{ color: '#FFFFFF', opacity: 0.8, fontSize: 14, marginBottom: 20 }}>
            {this.state.error?.message || 'An unexpected error occurred'}
          </p>
          <button 
            onClick={() => {
              localStorage.clear()
              window.location.reload()
            }}
            style={{
              padding: '12px 24px',
              borderRadius: 20,
              background: '#FF3B30',
              border: 'none',
              color: '#FFFFFF',
              fontWeight: 600
            }}
          >
            Clear Data & Restart
          </button>
        </div>
      )
    }

    return this.props.children
  }
}
